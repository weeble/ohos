using System;
using System.IO;
using Mono.Addins;
using ohWidget.Utils;
using OpenHome.Os.AppManager;
using OpenHome.Os.Platform;
using OpenHome.Widget.Nodes;
//using OpenHome.Widget.Nodes.Combined;
using OpenHome.Widget.Nodes.Logging;
//using OpenHome.Widget.Protocols.SimpleUpnp;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.Core;
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
    }

    public class Program
    {
        static ILog Logger = LogManager.GetLogger(typeof(Program));
        static void Main(string[] aArgs)
        {
            OptionParser parser = new OptionParser(aArgs);
            OptionParser.OptionString optionConfigFile = new OptionParser.OptionString("-c", "--config", null, "Configuration file location.", "CONFIG");
            parser.AddOption(optionConfigFile);
            OptionParser.OptionString optionInstallFile = new OptionParser.OptionString("-i", "--install", null, "App to install.", "APPFILE");
            parser.AddOption(optionInstallFile);
            /*OptionParser.OptionBool optionNoLoopback = new OptionParser.OptionBool("-p", "--publish", "Advertise this node on the network (default is to use loopback only)");
            parser.AddOption(optionNoLoopback);
            OptionParser.OptionString optionUuid = new OptionParser.OptionString("-u", "--uuid", "", "Set a uuid for the Node (default is to use an auto-generated guid)", "");
            parser.AddOption(optionUuid);
            OptionParser.OptionString optionUiDir = new OptionParser.OptionString("-d", "--ui-dir", "", "Absolute path to read Web UI files from", "");
            parser.AddOption(optionUiDir);
            OptionParser.OptionString optionZigBeeSerial = new OptionParser.OptionString("-z", "--zigbee-serial", null, "Serial device for Telegesis module", "");
            parser.AddOption(optionZigBeeSerial);
            OptionParser.OptionString optionZWaveSerial = new OptionParser.OptionString("-w", "--zwave-serial", null, "Serial device for aeon z-stick module", "");
            parser.AddOption(optionZWaveSerial);
            OptionParser.OptionBool optionMdns = new OptionParser.OptionBool("-m", "--mdns", "Enable lookup of node using multicast DNS");
            parser.AddOption(optionMdns);
            OptionParser.OptionBool optionDisableSimpleUpnp = new OptionParser.OptionBool("-U", "--disable-simpleupnp", "Disable UPnP widget discovery.");
            parser.AddOption(optionDisableSimpleUpnp);
            OptionParser.OptionString optionSimpleUpnpDrivers = new OptionParser.OptionString(null, "--simpleupnp-drivers", "", "Widget types for which to load Simple UPnP drivers. (Comma separated list, possible values: BinaryLight,DimmableLight,TestDataTypes,Thermometer.)", "DRIVERS");
            parser.AddOption(optionSimpleUpnpDrivers);
            OptionParser.OptionString optionStoreDirectory = new OptionParser.OptionString("-s", "--store", null, "Directory to store persisted widgets.", "");
            parser.AddOption(optionStoreDirectory);
            OptionParser.OptionString optionUpdateDir = new OptionParser.OptionString(null, "--update-dir", null, "Directory containing update configuration files.", "");
            parser.AddOption(optionUpdateDir);
            OptionParser.OptionBool optionEnableUpdateReboot = new OptionParser.OptionBool(null, "--reboot-on-update", "Automatically reboot to apply an installed update.");
            parser.AddOption(optionEnableUpdateReboot);
            OptionParser.OptionUint optionWebSocketPort = new OptionParser.OptionUint(null, "--ws-port", 54321, "Port for WebSocket server.", "");
            parser.AddOption(optionWebSocketPort);
            OptionParser.OptionBool optionNoGui = new OptionParser.OptionBool(null, "--no-gui", "Disable serving of the web GUI. Prevents overwriting of \"Node.js\".");
            parser.AddOption(optionNoGui);
            OptionParser.OptionBool optionSuppressErrorDialogs = new OptionParser.OptionBool(null, "--suppress-error-dialogs", "Suppress popups when a fatal error occurs. (Windows only.)");
            parser.AddOption(optionSuppressErrorDialogs);
            OptionParser.OptionString optionLogFile = new OptionParser.OptionString("-l", "--logfile", null, "Specify log file.", "");
            parser.AddOption(optionLogFile);
            OptionParser.OptionString optionLogLevel = new OptionParser.OptionString(null, "--loglevel", null, "Override default log level. Choose: DEBUG, INFO, WARN, ERROR, FATAL.", "");
            parser.AddOption(optionLogLevel);
            OptionParser.OptionBool optionNoPrompt = new OptionParser.OptionBool(null, "--no-prompt", "Suppress display of the prompt.");
            parser.AddOption(optionNoPrompt);
            OptionParser.OptionBool optionNoConsole = new OptionParser.OptionBool(null, "--no-console", "Don't use a console.");
            parser.AddOption(optionNoConsole);*/
            parser.Parse();
            if (parser.HelpSpecified())
                return;

            string configFilename = optionConfigFile.Value ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "ohOs" + Path.DirectorySeparatorChar + "ohos.config.xml";
            ConfigFileCollection config = new ConfigFileCollection(new[] { configFilename });
            IConfigFileCollection sysConfig = config.GetSubcollection("/system-settings");

            string storeDirectory =
                sysConfig.GetElementValueAsFilepath("store") ??
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
            if (sysConfig.GetAttributeAsBoolean("errors/native/@dialog") ?? 
                (noErrorDialogs != null && noErrorDialogs != "0"))
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    ErrorHandling.SuppressWindowsErrorDialogs();
                }
            }

            InitParams initParams = new InitParams();
            if (sysConfig.GetAttributeAsBoolean("network/@loopback") ?? true)
            {
                if (sysConfig.GetAttributeAsBoolean("mdns/@enable") ?? true)
                {
                    Console.WriteLine("ERROR: cannot usefully enable mdns with loopback");
                    throw new Exception();
                }
                initParams.UseLoopbackNetworkAdapter = true;
            }
            bool wsEnabled = sysConfig.GetAttributeAsBoolean("websockets/@enable") ?? true;
            uint wsPort = uint.Parse(sysConfig.GetAttributeValue("websockets/@port") ?? "54321");
            initParams.DvNumWebSocketThreads = 10; // max 10 web based control points
            initParams.DvWebSocketPort = wsEnabled ? wsPort : 0;
            initParams.NumActionInvokerThreads = 8;
            initParams.DvNumServerThreads = 8;
            initParams.TcpConnectTimeoutMs = 1000; // NOTE: Defaults to 500ms. At that value, we miss a lot of nodes during soak and stress tests.
            if (config.GetAttributeAsBoolean("mdns/@enable") ?? true)
            {
                initParams.DvEnableBonjour = true;
            }
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
                    string nodeGuid = (sysConfig.GetElementValue("uuid") ?? "").Trim();
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
                    commandDispatcher.AddCommand("exit", aArguments => consoleInterface.Running = false, "Stop and close this OpenHome Node process.");
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
                    consoleInterface.Prompt = (sysConfig.GetAttributeAsBoolean("console/@prompt") ?? true) ? "OpenHome>" : "";

                    AppServices services = new AppServices()
                    {
                        //StorePath = storeDirectory,
                        NodeInformation = new NodeInformation{
                            WebSocketPort = wsEnabled ? wsPort : (uint?)null,
                            MultiNodeEnabled = sysConfig.GetAttributeAsBoolean("multinode/@enable") ?? false
                        },
                        CommandRegistry = commandDispatcher,
                        CpDeviceListFactory = deviceListFactory,
                        DeviceFactory = deviceFactory,
                        LogController = logSystem.LogController,
                        LogReader = logSystem.LogReader,
                        NodeRebooter = null,
                        UpdateService = null
                    };

                    Console.WriteLine(storeDirectory);
                    using (var appManager = new Manager(storeDirectory, services, config))
                    {
                        if (optionInstallFile.Value != null)
                        {
                            appManager.Install(optionInstallFile.Value);
                        }
                        using (var appController = new AppController(nodeGuid))
                        {
                            //string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                            //appManager.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.zip"));
                            if (!(sysConfig.GetAttributeAsBoolean("console/@enable") ?? true))
                            {
                                WaitForever();
                            }
                            else
                            {
                                RunConsole(consoleInterface, sysConfig.GetAttributeAsBoolean("console/@prompt") ?? true);
                            }
                            Logger.Info("Shutting down node...");
                            if (sysConfig.GetAttributeAsBoolean("console/@enable") ?? true)
                            {
                                Console.WriteLine("Shutting down node...");
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
        }

        private static void RunConsole(ConsoleInterface aConsoleInterface, bool aSilent)
        {
            if (!aSilent)
            {
                Console.WriteLine("Type exit to quit.");
            }
            aConsoleInterface.RunConsole();
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

                WaitForever();

                // We will never exit the WaitOne.
            }
        }

        private static void WaitForever()
        {
            System.Threading.Semaphore sem = new System.Threading.Semaphore(0, 1);
            sem.WaitOne();
        }
    }

}
