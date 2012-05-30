using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using OpenHome.Os.Apps;
using OpenHome.Os.Host.Guardians;
using OpenHome.Os.Platform;
using OpenHome.Os.Platform.Clock;
using OpenHome.Os.Platform.Logging;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.Core;
using OpenHome.Os.Platform.Threading;
using OpenHome.Net.Device;
using log4net;
using OpenHome.Os.Core;
using OpenHome.Os.Update;

//using Mono.Addins;

//[assembly: AddinRoot("ohOs", "1.1")]

namespace OpenHome.Os.Host
{
    class NullBootControl : IBootControl
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(NullBootControl));

        public BootMode Current
        {
            get { return BootMode.eRfs0; }
        }

        public BootMode Pending
        {
            get { return BootMode.eRfs0; }
            set
            {
                Logger.WarnFormat("NullBootControl: BootMode changed. If I were a Sheevaplug, next reboot I would be in {0} mode.", value);
            }
        }
    }

    public class AppServices : IAppServices
    {
        //public string StorePath { get; set; }
        public INodeDeviceAccessor NodeDeviceAccessor { get; set; }
        public INodeInformation NodeInformation { get; set; }
        public IDvDeviceFactory DeviceFactory { get; set; }
        public ICpUpnpDeviceListFactory CpDeviceListFactory { get; set; }
        public INodeRebooter NodeRebooter { get; set; }
        public IUpdateService UpdateService { get; set; }
        public ICommandRegistry CommandRegistry { get; set; }
        public ILogReader LogReader { get; set; }
        public ILogController LogController { get; set; }
        public ISystemClock SystemClock { get; set; }

        readonly Dictionary<Type, object> iAdditionalServices = new Dictionary<Type, object>();
        public void RegisterService<T>(T aService)
        {
            iAdditionalServices.Add(typeof(T), aService);
        }

        public T ResolveService<T>()
        {
            object service;
            if (iAdditionalServices.TryGetValue(typeof(T), out service))
            {
                return (T)service;
            }
            if (typeof(T).IsAssignableFrom(typeof(INodeDeviceAccessor))) { return (T)NodeDeviceAccessor; }
            if (typeof(T).IsAssignableFrom(typeof(INodeInformation))) { return (T)NodeInformation; }
            if (typeof(T).IsAssignableFrom(typeof(IDvDeviceFactory))) { return (T)DeviceFactory; }
            if (typeof(T).IsAssignableFrom(typeof(ICpUpnpDeviceListFactory))) { return (T)CpDeviceListFactory; }
            if (typeof(T).IsAssignableFrom(typeof(INodeRebooter))) { return (T)NodeRebooter; }
            if (typeof(T).IsAssignableFrom(typeof(IUpdateService))) { return (T)UpdateService; }
            if (typeof(T).IsAssignableFrom(typeof(ICommandRegistry))) { return (T)CommandRegistry; }
            if (typeof(T).IsAssignableFrom(typeof(ILogReader))) { return (T)LogReader; }
            if (typeof(T).IsAssignableFrom(typeof(ILogController))) { return (T)LogController; }
            throw new ArgumentException(String.Format("No service registered for type {0}.", typeof(T)));
        }
    }

    class NodeInformation : INodeInformation
    {
        public uint? WebSocketPort { get; set; }
        public bool MultiNodeEnabled { get; set; }
        public uint DvServerPort { get; set; }
    }

    class NodeRebooter : INodeRebooter
    {
        static protected readonly ILog Logger = LogManager.GetLogger(typeof(NodeRebooter));
        readonly ConsoleInterface iConsole;
        readonly Channel<int> iExitChannel;

        public NodeRebooter(ConsoleInterface aConsole, Channel<int> aExitChannel)
        {
            iConsole = aConsole;
            iExitChannel = aExitChannel;
        }

        public virtual void RebootNode()
        {
            iConsole.Quit(10);
            iExitChannel.Send(10);
        }
        public virtual void SoftRestartNode()
        {
            iConsole.Quit(9);
            iExitChannel.Send(9);
        }
    }

    class LinuxNodeRebooter : NodeRebooter
    {
        public LinuxNodeRebooter(ConsoleInterface aConsole, Channel<int> aExitChannel) : base(aConsole, aExitChannel)
        {
        }

        public override void RebootNode()
        {
            try
            {
                Process.Start("reboot");
            }
            catch (Exception e)
            {
                Logger.Error("Failed to reboot.", e);
            }
            base.RebootNode();
        }
    }

    public enum ExitCodes
    {
        NormalExit = 0,
        SoftRestart = 9,
        HardReboot = 10,
        GuardianDied = 7,
    }

    public class Program
    {
        private class Options
        {
            public OptionParser.OptionString ConfigFile { get; private set; }
            public OptionParser.OptionString InstallFile { get; private set; }
            public OptionParser.OptionString Subprocess { get; private set; }
            public OptionParser.OptionBool SingleProcess { get; private set; }
            public OptionParser.OptionString LogLevel { get; private set; }
            public OptionParser.OptionString Uuid { get; private set; }
            public Options()
            {
                ConfigFile = new OptionParser.OptionString("-c", "--config", null, "Configuration file location.", "CONFIG");
                InstallFile = new OptionParser.OptionString("-i", "--install", null, "Install the given app and exit.", "APPFILE");
                Subprocess = new OptionParser.OptionString(null, "--subprocess", null, "Reserved.", "SUBPROCESSDATA");
                SingleProcess = new OptionParser.OptionBool(null, "--single-process", "Run as a single process. Disables soft restarts.");
                LogLevel = new OptionParser.OptionString(null, "--loglevel", null, "Set default log level.", "LOGLEVEL");
                Uuid = new OptionParser.OptionString(null, "--udn", null, "Override UDN.", "UDN");
            }
            public OptionParser Parse(string[] aArgs)
            {
                OptionParser parser = new OptionParser(aArgs);
                parser.AddOption(ConfigFile);
                parser.AddOption(InstallFile);
                parser.AddOption(Subprocess);
                parser.AddOption(SingleProcess);
                parser.AddOption(LogLevel);
                parser.AddOption(Uuid);
                parser.Parse();
                return parser;
            }
        }
        static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
        static readonly ILog OhNetLogger = LogManager.GetLogger("OpenHome.Net");
        static int Main(string[] aArgs)
        {
            Options options = new Options();
            OptionParser parser = options.Parse(aArgs);
            if (parser.HelpSpecified())
                return (int)ExitCodes.NormalExit;

            if (options.SingleProcess.Value || options.Subprocess.Value != null)
            {
                return RunAsMainProcess(options);
            }
            return RunAsGuardianProcess(aArgs, options);
        }

        static int RunAsGuardianProcess(string[] aArgs, Options aOptions)
        {
            // Guardian process is responsible for starting the main process
            // and restarting it when necessary.
            IConfigFileCollection sysConfig;
            ConfigFileCollection config;
            LoadConfig(aOptions, out config, out sysConfig);
            string storeDirectory = SetupStore(sysConfig);
            /* LogSystem logSystem = */ SetupLogging(storeDirectory, config, null);
            Logger.Info("Guardian process starting.");

            Guardian guardian = new Guardian(Path.Combine(storeDirectory, "fifos"))
            {
                FailureWindow = TimeSpan.FromSeconds(60),
                MaxFailures = 3,
                Logger = Logger,
                RetryPause = TimeSpan.FromSeconds(5),
                WhenChildExitsWithCode = aExitCode =>
                    {
                        if (aExitCode == (int)ExitCodes.SoftRestart) return ExitBehaviour.Repeat;
                        if (aExitCode == (int)ExitCodes.NormalExit) return ExitBehaviour.Exit;
                        if (aExitCode == (int)ExitCodes.HardReboot) return ExitBehaviour.Exit;
                        return ExitBehaviour.Retry;
                    }
            };
            return guardian.Run(
                aToken =>
                {
                    List<string> childArgs = new List<string> { "--subprocess", aToken }; //, handle1 + "," + handle2 };
                    childArgs.AddRange(aArgs);
                    var p = Process.Start(
                        new ProcessStartInfo(
                            Assembly.GetExecutingAssembly().Location,
                            string.Join(" ", childArgs.ToArray())
                        )
                        {
                            UseShellExecute = false
                        }
                    );
                    return p;
                });
        }
        
        private static IUpdateService CreateUpdateService(INodeRebooter aNodeRebooter, bool aSheevaMode)
        {
            var updater = new Updater();

            if (aSheevaMode)
            {
                var bootControl = new BootControlSheeva();
                return new UpdateService(updater, bootControl, aNodeRebooter);
            }
            else
            {
                var bootControl = new NullBootControl();
                return new UpdateService(updater, bootControl, aNodeRebooter);
            }
        }

        static int RunAsMainProcess(Options aOptions)
        {
            int exitCode = 0;
            Channel<int> exitChannel = new Channel<int>(1);
            GuardianChild guardianChild = null;
            IConfigFileCollection sysConfig;
            ConfigFileCollection config;
            LoadConfig(aOptions, out config, out sysConfig);

            string storeDirectory = SetupStore(sysConfig);

            if (aOptions.Subprocess.Value != null && aOptions.Subprocess.Value != "nopipe")
            {
                guardianChild = new GuardianChild(Path.Combine(storeDirectory, "fifos"));
                guardianChild.Start(aOptions.Subprocess.Value);
            }

            LogSystem logSystem = SetupLogging(storeDirectory, config, aOptions.LogLevel.Value);
            Logger.Info("Node starting.");

            string noErrorDialogs = Environment.GetEnvironmentVariable("OPENHOME_NO_ERROR_DIALOGS");
            if (sysConfig.GetAttributeAsBoolean(e=>e.Elements("errors").Elements("native").Attributes("dialog").FirstOrDefault()) ?? 
                (noErrorDialogs != null && noErrorDialogs != "0"))
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    ErrorHandling.SuppressWindowsErrorDialogs();
                }
            }

            InitParams initParams = new InitParams();
            if (sysConfig.GetAttributeAsBoolean(e=>e.Elements("network").Attributes("loopback").FirstOrDefault()) ?? true)
            {
                initParams.UseLoopbackNetworkAdapter = true;
            }
            bool wsEnabled = sysConfig.GetAttributeAsBoolean(e=>e.Elements("websockets").Attributes("enable").FirstOrDefault()) ?? true;
            uint wsPort = uint.Parse(sysConfig.GetAttributeValue(e=>e.Elements("websockets").Attributes("port").FirstOrDefault()) ?? "54321");
            string updateConfigFile = sysConfig.GetElementValueAsFilepath(e => e.Element("system-update-config"));
            initParams.DvNumWebSocketThreads = 10; // max 10 web based control points
            initParams.DvWebSocketPort = wsEnabled ? wsPort : 0;
            initParams.DvUpnpWebServerPort = 55178; // remote access requires that we select a port.  This value can be changed without updating remote access code however.
            initParams.NumActionInvokerThreads = 8;
            initParams.DvNumServerThreads = 8;
            initParams.TcpConnectTimeoutMs = 1000; // NOTE: Defaults to 500ms. At that value, we miss a lot of nodes during soak and stress tests.
            InitParams.OhNetCallbackMsg logAction = (aPtr, aMessage)=> OhNetLogger.Warn(aMessage); // Assume OhNet messages are warnings.
            initParams.LogOutput = logAction;
            if (sysConfig.GetAttributeAsBoolean(e=>e.Elements("mdns").Attributes("enable").FirstOrDefault()) ?? (!initParams.UseLoopbackNetworkAdapter))
            {
                initParams.DvEnableBonjour = true;
            }
            uint dvServerPort = initParams.DvUpnpWebServerPort;
            //UpdateServiceMode updateMode =
            //    (optionUpdateDir.Value==null) ?
            //        UpdateServiceMode.Disabled :
            //        optionEnableUpdateReboot.Value ?
            //            UpdateServiceMode.Full :
            //            UpdateServiceMode.Pretend;
            using (Library library = Library.Create(initParams))
            {
                try
                {
                    SubnetList subnetList = new SubnetList();
                    NetworkAdapter nif = subnetList.SubnetAt(0);
                    uint subnet = nif.Subnet();
                    subnetList.Dispose();
                    var combinedStack = library.StartCombined(subnet);
                    var deviceListFactory = new CpUpnpDeviceListFactory(combinedStack.ControlPointStack);
                    var deviceFactory = new DvDeviceFactory(combinedStack.DeviceStack);
                    string nodeGuid;
                    if (string.IsNullOrEmpty(aOptions.Uuid.Value))
                    {
                        nodeGuid = (sysConfig.GetElementValue(e => e.Elements("uuid").FirstOrDefault()) ?? "").Trim();
                    }
                    else
                    {
                        nodeGuid = aOptions.Uuid.Value;
                    }
                    if (nodeGuid.Length == 0)
                    {
                        nodeGuid = Guid.NewGuid().ToString();
                    }

                    var commandDispatcher = new CommandDispatcher();
                    var consoleInterface = new ConsoleInterface(commandDispatcher);
                    // TODO: Use an appropriate rebooter on non-Linux systems.
                    var nodeRebooter = new LinuxNodeRebooter(consoleInterface, exitChannel);
                    if (guardianChild != null)
                    {
                        guardianChild.WhenGuardianTerminates +=
                            (aSender, aEvent) =>
                            {
                                consoleInterface.Quit((int)ExitCodes.GuardianDied);
                                exitChannel.NonBlockingSend((int)ExitCodes.GuardianDied);
                            };
                    }
                    commandDispatcher.AddCommand("exit", aArguments =>
                        {
                            if (aArguments == "")
                            {
                                consoleInterface.Quit((int)ExitCodes.NormalExit);
                            }
                            else
                            {
                                int exitCodeArg;
                                if (!int.TryParse(aArguments, out exitCodeArg))
                                {
                                    Console.WriteLine("Unrecognized exit code.");
                                    return;
                                }
                                consoleInterface.Quit(exitCodeArg);
                            }
                        }, "Stop and close this OpenHome Node process.");
                    commandDispatcher.AddCommand("restart", aArguments => consoleInterface.Quit((int)ExitCodes.SoftRestart), "Restart ohOs process.");
                    commandDispatcher.AddCommand("help", aArguments => Console.WriteLine(commandDispatcher.DescribeAllCommands()), "Show a list of available commands.");
                    commandDispatcher.AddCommand("logdump", aArguments => Console.WriteLine(logSystem.LogReader.GetLogTail(100000)), "Dump the current contents of the logfile.");
                    commandDispatcher.AddCommand("log", aArguments => Logger.Debug(aArguments), "Add a message to the log.");
                    commandDispatcher.AddCommand("loginfo", aArguments =>
                                                                {
                                                                    foreach (var kvp in logSystem.LogController.GetLogLevels())
                                                                    {
                                                                        Console.WriteLine("{0}:{1}", kvp.Key, kvp.Value);
                                                                    }
                                                                }, "Show log levels");
                    commandDispatcher.AddCommand("logset", aArguments =>
                                                               {
                                                                   string[] args = aArguments.Split(new[]{' '}, 2);
                                                                   logSystem.LogController.SetLogLevel(args[0], args[1]);
                                                               }, "Show log levels");

                    commandDispatcher.AddCommand("crash", aArguments => { throw new Exception("Crash requested."); }, "Crash the node.");
                    commandDispatcher.UnrecognizedCommandHandler = (aCommand, aArguments) =>
                                                                       {
                                                                           if (aCommand != "") { Console.WriteLine("Unknown command. Type exit to quit."); }
                                                                       };
                    consoleInterface.Prompt = (sysConfig.GetAttributeAsBoolean(e=>e.Elements("console").Attributes("prompt").FirstOrDefault()) ?? true) ? "OpenHome>" : "";

                    var updateService = CreateUpdateService(nodeRebooter, false);
                    var systemClock = new LinuxSystemClock();
                    var clockProvider = new SystemClockProvider(systemClock);
                    var logControlProvider = new LogControlProvider(logSystem.LogReader, logSystem.LogController);

                    using (var nodeDevice = new NodeDevice(nodeGuid))
                    using (new ProviderSystemUpdate(nodeDevice.Device.RawDevice, updateService,
                        updateConfigFile, Path.Combine(storeDirectory, "updates", "UpdateService.xml")))
                    using (new ProviderNode(nodeDevice.Device.RawDevice, clockProvider, logControlProvider))
                    {
                        AppServices services = new AppServices
                                                   {
                                                       //StorePath = storeDirectory,
                                                       NodeInformation = new NodeInformation
                                                       {
                                                           WebSocketPort = wsEnabled ? wsPort : (uint?)null,
                                                           MultiNodeEnabled = sysConfig.GetAttributeAsBoolean(e => e.Elements("multinode").Attributes("enable").FirstOrDefault()) ?? false,
                                                           DvServerPort = dvServerPort
                                                       },
                                                       CommandRegistry = commandDispatcher,
                                                       CpDeviceListFactory = deviceListFactory,
                                                       DeviceFactory = deviceFactory,
                                                       LogController = logSystem.LogController,
                                                       LogReader = logSystem.LogReader,
                                                       NodeRebooter = nodeRebooter,
                                                       UpdateService = null,
                                                       NodeDeviceAccessor = nodeDevice,
                                                       SystemClock = systemClock
                                                   };

                        Console.WriteLine(storeDirectory);
                        using (var appModule = new AppShellModule(services, config, nodeGuid))
                        {
                            services.RegisterService(appModule.AppShell);
                            var appManager = appModule.AppShell;
                            if (aOptions.InstallFile.Value != null)
                            {
                                appManager.Install(aOptions.InstallFile.Value);
                            }
                            else
                            {
                                var appManagerConsoleCommands = new AppManagerConsoleCommands(appManager);
                                appManagerConsoleCommands.Register(commandDispatcher);
                                appManager.Start();
                                using (new AppListService(nodeDevice.Device.RawDevice, appManager))
                                {
                                    //commandDispatcher.AddCommand("bump", aArguments => appController.BumpDummySequenceNumber(), "Bump the sequence number for the dummy app, for testing.");
                                    //string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                    //appManager.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.zip"));
                                    if (!(sysConfig.GetAttributeAsBoolean(e => e.Elements("console").Attributes("enable").FirstOrDefault()) ?? true))
                                    {
                                        exitCode = WaitForExit(exitChannel);
                                    }
                                    else
                                    {
                                        exitCode = RunConsole(consoleInterface, sysConfig.GetAttributeAsBoolean(e => e.Element("console").Attribute("prompt")) ?? true, exitChannel);
                                    }
                                    Logger.Info("Shutting down node...");
                                    if (sysConfig.GetAttributeAsBoolean(e => e.Elements("console").Attributes("enable").FirstOrDefault()) ?? true)
                                    {
                                        Console.WriteLine("Shutting down node...");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Fatal(e);
                    throw;
                }
            }
            Logger.Info("Shutdown complete.");
            return exitCode;
        }

        static LogSystem SetupLogging(string storeDirectory, ConfigFileCollection config, string logLevel)
        {
            string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string logConfigFile = Path.Combine(exeDirectory, "Log4Net.config");
            var logSystem = Log4Net.SetupLog4NetLogging(
                logConfigFile,
                Path.Combine(Path.Combine(storeDirectory, "logging"), "ohos.log"),
                Path.Combine(Path.Combine(storeDirectory, "logging"), "loglevels.xml"));
            if (logLevel != null)
            {
                logSystem.LogController.SetLogLevel("ROOT", logLevel);
            }
            logSystem.LogController.SetLogLevel("OpenHome.Os.Host.Program", "DEBUG");
            //if (optionLogLevel.Value != null)
            //{
            //    logSystem.LogController.SetLogLevel("ROOT", optionLogLevel.Value);
            //}

            config.LogErrors(Logger);
            return logSystem;
        }

        static string SetupStore(IConfigFileCollection sysConfig)
        {
            return sysConfig.GetElementValueAsFilepath(e=>e.Element("store")) ??
                (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "ohOs");
            //DirectoryInfo storeDirectory = Directory.CreateDirectory(path);
        }

        const string ConfigExtension = ".ohconfig.xml";

        static void LoadConfig(Options aOptions, out ConfigFileCollection aConfig, out IConfigFileCollection aSysConfig)
        {
            string exeDir;
            string exeName;
            string exeConfigFilename;
            try
            {
                string assemblyPath = new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath;
                exeDir = Path.GetDirectoryName(assemblyPath);
                exeName = Path.GetFileNameWithoutExtension(assemblyPath);
            }
            catch (InvalidOperationException)
            {
                exeDir = null;
                exeName = "ohOs.Host";
            }

            if (exeDir == null)
            {
                exeConfigFilename = null;
            }
            else
            {
                exeConfigFilename = Path.Combine(exeDir, exeName + ConfigExtension);
            }
            
            string userConfigFilename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "ohOs" + Path.DirectorySeparatorChar + exeName + ConfigExtension;

            List<string> configFiles = new List<string>();
            if (aOptions.ConfigFile.Value != null)
            {
                configFiles.Add(aOptions.ConfigFile.Value);
            }
            else if (!string.IsNullOrEmpty(exeConfigFilename) && File.Exists(exeConfigFilename))
            {
                configFiles.Add(exeConfigFilename);
            }
            else if (File.Exists(userConfigFilename))
            {
                configFiles.Add(userConfigFilename);
            }
            aConfig = new ConfigFileCollection(configFiles);
            aSysConfig = aConfig.GetSubcollection(e=>e.Element("system-settings"));
        }

        private static int RunConsole(ConsoleInterface aConsoleInterface, bool aSilent, Channel<int> aExitChannel)
        {
            if (!aSilent)
            {
                Console.WriteLine("Type exit to quit.");
            }
            int exitCode = aConsoleInterface.RunConsole();
            if (aConsoleInterface.EndOfInput)
            {
                Console.WriteLine();
                // Our input has ended. Should we quit? I'll guess yes, but we
                // might want to review this. Would we ever want to run a node
                // and close its input stream (or redirect from a file) before
                // we want the node to stop?
                // However, we lack flags to daemonize our process. Until we
                // have a proper daemon mode, we should tolerate our input
                // ending.

                exitCode = WaitForExit(aExitChannel);

                // We will never exit the WaitOne.
            }
            return exitCode;
        }

        private static int WaitForExit(Channel<int> aExitChannel)
        {
            return aExitChannel.Receive();
        }
    }

}
