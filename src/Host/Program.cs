using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
//using Mono.Addins;
using ohWidget.Utils;
using OpenHome.Os.AppManager;
using OpenHome.Os.Platform;
using OpenHome.Widget.Nodes;
//using OpenHome.Widget.Nodes.Combined;
using OpenHome.Widget.Nodes.Logging;
//using OpenHome.Widget.Protocols.SimpleUpnp;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.Core;
using OpenHome.Widget.Nodes.Threading;
using OpenHome.Widget.Utils;
using OpenHome.Net.Device;
using log4net;

//[assembly: AddinRoot("ohOs", "1.1")]

namespace Node
{

    public class AppServices : IAppServices
    {
        //public string StorePath { get; set; }
        public INodeInformation NodeInformation { get; set; }
        public IDvDeviceFactory DeviceFactory { get; set; }
        public ICpUpnpDeviceListFactory CpDeviceListFactory { get; set; }
        public INodeRebooter NodeRebooter { get; set; }
        public IUpdateService UpdateService { get; set; }
        public ICommandRegistry CommandRegistry { get; set; }
        public ILogReader LogReader { get; set; }
        public ILogController LogController { get; set; }
        public object ResolveService<T>()
        {
            if (typeof(T).IsAssignableFrom(typeof(INodeInformation))) { return NodeInformation; }
            if (typeof(T).IsAssignableFrom(typeof(IDvDeviceFactory))) { return DeviceFactory; }
            if (typeof(T).IsAssignableFrom(typeof(ICpUpnpDeviceListFactory))) { return CpDeviceListFactory; }
            if (typeof(T).IsAssignableFrom(typeof(INodeRebooter))) { return NodeRebooter; }
            if (typeof(T).IsAssignableFrom(typeof(IUpdateService))) { return UpdateService; }
            if (typeof(T).IsAssignableFrom(typeof(ICommandRegistry))) { return CommandRegistry; }
            if (typeof(T).IsAssignableFrom(typeof(ILogReader))) { return LogReader; }
            if (typeof(T).IsAssignableFrom(typeof(ILogController))) { return LogController; }
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
        readonly ConsoleInterface iConsole;
        readonly Channel<int> iExitChannel;

        public NodeRebooter(ConsoleInterface aConsole, Channel<int> aExitChannel)
        {
            iConsole = aConsole;
            iExitChannel = aExitChannel;
        }

        public void RebootNode()
        {
            iConsole.Quit(10);
            iExitChannel.Send(10);
        }
        public void SoftRestartNode()
        {
            iConsole.Quit(9);
            iExitChannel.Send(9);
        }
    }

    public class Program
    {
        private class Options
        {
            public OptionParser.OptionString ConfigFile { get; private set; }
            public OptionParser.OptionString InstallFile { get; private set; }
            public OptionParser.OptionString Subprocess { get; private set; }
            public Options()
            {
                ConfigFile = new OptionParser.OptionString("-c", "--config", null, "Configuration file location.", "CONFIG");
                InstallFile = new OptionParser.OptionString("-i", "--install", null, "Install the given app and exit.", "APPFILE");
                Subprocess = new OptionParser.OptionString(null, "--subprocess", null, "Reserved.", "SUBPROCESSDATA");
            }
            public OptionParser Parse(string[] aArgs)
            {
                OptionParser parser = new OptionParser(aArgs);
                parser.AddOption(ConfigFile);
                parser.AddOption(InstallFile);
                parser.AddOption(Subprocess);
                parser.Parse();
                return parser;
            }
        }
        static ILog Logger = LogManager.GetLogger(typeof(Program));
        static int Main(string[] aArgs)
        {
            Options options = new Options();
            OptionParser parser = options.Parse(aArgs);
            if (parser.HelpSpecified())
                return 0;

            return RunAsMainProcess(options);

            // Subprocess code doesn't work on Linux.
            //if (options.Subprocess.Value == null)
            //{
            //    RunAsGuardianProcess(aArgs);
            //}
            //else
            //{
            //    RunAsMainProcess(options);
            //}
        }

        static void RunAsGuardianProcess(string[] aArgs)
        {
            // Guardian process is responsible for starting the main process,
            // monitoring crashes and restarting it when necessary.
            for (; ; )
            {
                int exitCode = RunChildProcess(aArgs);
                if (exitCode == 0)
                {
                    return;
                }
                Console.WriteLine("Restarting child process in 5s...");
                Thread.Sleep(5000);
            }
        }

        static int RunChildProcess(string[] aArgs)
        {
            var guardianToChildStream = new System.IO.Pipes.AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            var childToGuardianStream = new System.IO.Pipes.AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            string handle1 = guardianToChildStream.GetClientHandleAsString();
            string handle2 = childToGuardianStream.GetClientHandleAsString();
            List<string> childArgs = new List<string> { "--subprocess", handle1 + "," + handle2 };
            childArgs.AddRange(aArgs);
            var startInfo = new ProcessStartInfo(
                System.Reflection.Assembly.GetExecutingAssembly().Location,
                string.Join(" ", childArgs.ToArray()))
                {
                    UseShellExecute = false,
                    
                };
            Process childProcess = Process.Start(startInfo);
            guardianToChildStream.DisposeLocalCopyOfClientHandle();
            childToGuardianStream.DisposeLocalCopyOfClientHandle();
            //Console.In.Close();
            //Console.Out.Close();
            //Console.Error.Close();
            using (var reader = new StreamReader(childToGuardianStream))
            {
                //childToGuardianStream.
                string output = reader.ReadToEnd();
                Console.WriteLine("Guardian received output ({0} chars):", output.Length);
                Console.WriteLine(output);
                Console.WriteLine("Guardian waiting for child to exit...");
                childProcess.WaitForExit();
                Console.WriteLine("Guardian saw child exit with code {0}.", childProcess.ExitCode);
                guardianToChildStream.Close();
                return childProcess.ExitCode;
            }

        }

        static int RunAsMainProcess(Options aOptions)
        {
            int exitCode = 0;
            Channel<int> exitChannel = new Channel<int>(1);
            if (aOptions.Subprocess.Value != null && aOptions.Subprocess.Value != "no")
            {
                string[] handleStrings = aOptions.Subprocess.Value.Split(new[] { ',' });
                if (handleStrings.Length != 2)
                {
                    throw new Exception("Bad --subprocess value");
                }
                /* var pipeFromGuardian = */ new System.IO.Pipes.AnonymousPipeClientStream(PipeDirection.In, handleStrings[0]);
                /* var pipeToGuardian = */ new System.IO.Pipes.AnonymousPipeClientStream(PipeDirection.Out, handleStrings[1]);
                throw new NotImplementedException();
            }
            string configFilename = aOptions.ConfigFile.Value ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "ohOs" + Path.DirectorySeparatorChar + "ohos.config.xml";
            ConfigFileCollection config = new ConfigFileCollection(new[] { configFilename });
            IConfigFileCollection sysConfig = config.GetSubcollection(e=>e.Element("system-settings"));

            string storeDirectory =
                sysConfig.GetElementValueAsFilepath(e=>e.Element("store")) ??
                    (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "ohOs");
            //DirectoryInfo storeDirectory = Directory.CreateDirectory(path);

            string exeDirectory = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            string logConfigFile = Path.Combine(exeDirectory, "Log4Net.config");
            var logSystem = OpenHome.Widget.Nodes.Logging.Log4Net.SetupLog4NetLogging(
                logConfigFile,
                Path.Combine(Path.Combine(storeDirectory, "logging"), "ohos.log"),
                Path.Combine(Path.Combine(storeDirectory, "logging"), "loglevels.xml"));
            //if (optionLogLevel.Value != null)
            //{
            //    logSystem.LogController.SetLogLevel("ROOT", optionLogLevel.Value);
            //}

            Logger.Info("Node started.");
            config.LogErrors(Logger);

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
                if (sysConfig.GetAttributeAsBoolean(e=>e.Elements("mdns").Attributes("enable").FirstOrDefault()) ?? true)
                {
                    Console.WriteLine("ERROR: cannot usefully enable mdns with loopback");
                    throw new Exception();
                }
                initParams.UseLoopbackNetworkAdapter = true;
            }
            bool wsEnabled = sysConfig.GetAttributeAsBoolean(e=>e.Elements("websockets").Attributes("enable").FirstOrDefault()) ?? true;
            uint wsPort = uint.Parse(sysConfig.GetAttributeValue(e=>e.Elements("websockets").Attributes("port").FirstOrDefault()) ?? "54321");
            initParams.DvNumWebSocketThreads = 10; // max 10 web based control points
            initParams.DvWebSocketPort = wsEnabled ? wsPort : 0;
            initParams.NumActionInvokerThreads = 8;
            initParams.DvNumServerThreads = 8;
            initParams.TcpConnectTimeoutMs = 1000; // NOTE: Defaults to 500ms. At that value, we miss a lot of nodes during soak and stress tests.
            if (sysConfig.GetAttributeAsBoolean(e=>e.Elements("mdns").Attributes("enable").FirstOrDefault()) ?? true)
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
                    subnetList.Destroy();
                    var combinedStack = library.StartCombined(subnet);
                    var deviceListFactory = new CpUpnpDeviceListFactory(combinedStack.ControlPointStack);
                    var deviceFactory = new DvDeviceFactory(combinedStack.DeviceStack);
                    string nodeGuid = (sysConfig.GetElementValue(e=>e.Elements("uuid").FirstOrDefault()) ?? "").Trim();
                    if (nodeGuid.Length == 0)
                    {
                        nodeGuid = Guid.NewGuid().ToString();
                    }
                    //string uiDir = optionUiDir.Value;
                    //if (optionNoGui.Value)
                    //{
                    //    uiDir = "";
                    //}
                    //else if (uiDir.Length == 0)
                    //{
                    //    uiDir = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "WebUi");
                    //}

                    var commandDispatcher = new CommandDispatcher();
                    var consoleInterface = new ConsoleInterface(commandDispatcher);
                    var nodeRebooter = new NodeRebooter(consoleInterface, exitChannel);
                    commandDispatcher.AddCommand("exit", aArguments => consoleInterface.Quit(0), "Stop and close this OpenHome Node process.");
                    commandDispatcher.AddCommand("help", aArguments => Console.WriteLine(commandDispatcher.DescribeAllCommands()), "Show a list of available commands.");
                    commandDispatcher.AddCommand("logdump", aArguments => Console.WriteLine(logSystem.LogReader.GetLogTail(10000)), "Dump the current contents of the logfile.");
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

                    AppServices services = new AppServices()
                                               {
                                                   //StorePath = storeDirectory,
                                                   NodeInformation = new NodeInformation{
                                                       WebSocketPort = wsEnabled ? wsPort : (uint?)null,
                                                       MultiNodeEnabled = sysConfig.GetAttributeAsBoolean(e=>e.Elements("multinode").Attributes("enable").FirstOrDefault()) ?? false,
                                                       DvServerPort = dvServerPort
                                                   },
                                                   CommandRegistry = commandDispatcher,
                                                   CpDeviceListFactory = deviceListFactory,
                                                   DeviceFactory = deviceFactory,
                                                   LogController = logSystem.LogController,
                                                   LogReader = logSystem.LogReader,
                                                   NodeRebooter = nodeRebooter,
                                                   UpdateService = null
                                               };

                    Console.WriteLine(storeDirectory);
                    using (var appModule = new ManagerModule(services, config))
                    {
                        var appManager = appModule.Manager;
                        if (aOptions.InstallFile.Value != null)
                        {
                            appManager.Install(aOptions.InstallFile.Value);
                        }
                        else
                        {
                            var appManagerConsoleCommands = new AppManagerConsoleCommands(appManager);
                            appManagerConsoleCommands.Register(commandDispatcher);
                            appManager.Start();
                            using (var appController = new AppController(nodeGuid))
                            {
                                commandDispatcher.AddCommand("bump", aArguments => appController.BumpDummySequenceNumber(), "Bump the sequence number for the dummy app, for testing.");
                                //string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                //appManager.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.zip"));
                                if (!(sysConfig.GetAttributeAsBoolean(e=>e.Elements("console").Attributes("enable").FirstOrDefault()) ?? true))
                                {
                                    exitCode = WaitForExit(exitChannel);
                                }
                                else
                                {
                                    exitCode = RunConsole(consoleInterface, sysConfig.GetAttributeAsBoolean(e=>e.Element("console").Attribute("prompt")) ?? true, exitChannel);
                                }
                                Logger.Info("Shutting down node...");
                                if (sysConfig.GetAttributeAsBoolean(e=>e.Elements("console").Attributes("enable").FirstOrDefault()) ?? true)
                                {
                                    Console.WriteLine("Shutting down node...");
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
