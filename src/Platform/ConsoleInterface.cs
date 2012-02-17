using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using OpenHome.Os.Platform;
using OpenHome.Os.Platform.Threading;

namespace ohWidget.Utils
{
    public class Command
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        readonly Action<string> iAction;
        public Command(string aName, Action<string> aAction, string aDescription)
        {
            Name = aName;
            iAction = aAction;
            Description = aDescription;
        }
        public void Invoke(string aArguments)
        {
            iAction(aArguments);
        }
    }
    public class CommandDispatcher : ICommandProcessor, ICommandRegistry
    {
        readonly Dictionary<string, Command> iCommands = new Dictionary<string, Command>();
        public Action<string,string> UnrecognizedCommandHandler { get; set; }
        public void AddCommand(string aName, Action<string> aAction, string aDescription)
        {
            if (iCommands.ContainsKey(aName))
            {
                throw new ArgumentException("Duplicate command name.");
            }
            iCommands[aName] = new Command(aName, aAction, aDescription);
        }
        public void ProcessCommand(string aCommand)
        {
            string command = aCommand.TrimStart();
            string[] splitCommand = command.TrimStart().Split(' ');
            if (splitCommand.Length >= 1)
            {
                string commandName = splitCommand[0];
                string arguments = String.Join(" ", splitCommand, 1, splitCommand.Length-1).TrimStart();
                if (iCommands.ContainsKey(commandName))
                {
                    iCommands[commandName].Invoke(arguments);
                }
                else
                {
                    if (UnrecognizedCommandHandler != null)
                    {
                        UnrecognizedCommandHandler(commandName, arguments);
                    }
                }
            }
            else
            {
                if (UnrecognizedCommandHandler != null)
                {
                    UnrecognizedCommandHandler("", "");
                }
            }
        }
        public string DescribeCommand(string aCommandName)
        {
            if (!iCommands.ContainsKey(aCommandName))
            {
                throw new KeyNotFoundException();
            }
            return iCommands[aCommandName].Description;
        }
        public string DescribeAllCommands()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var kvp in iCommands.OrderBy((aItem) => aItem.Key))
            {
                sb.AppendFormat("{0}:\n    {1}\n", kvp.Key, kvp.Value.Description);
            }
            return sb.ToString();
        }
    }


    public interface ICommandProcessor
    {
        void ProcessCommand(string aCommand);
    }
    public class ConsoleInterface
    {
        public string Prompt { get; set; }
        public bool Running { get; set; }
        public bool EndOfInput { get; private set; }
        readonly ICommandProcessor iCommandProcessor;
        readonly Channel<int> iQuitChannel = new Channel<int>(1);
        public ConsoleInterface(ICommandProcessor aCommandProcessor)
        {
            Prompt = ">";
            Running = true;
            EndOfInput = false;
            iCommandProcessor = aCommandProcessor;
        }
        /// <summary>
        /// Abandon the console and return from RunConsole with the
        /// provided exit code. Note that the console thread will
        /// not terminate immediately - it is trapped inside a blocking
        /// call to Console.ReadLine(). However, it is set as a background
        /// thread, so it will not prevent process shutdown.
        /// </summary>
        /// <remarks>
        /// This operation can be safely invoked on a different thread
        /// from RunConsole(). If invoked multiple times from the same
        /// thread, only the first exit code will be used. The exit is
        /// still guaranteed if multiple threads invoke Quit, but it can
        /// no longer be known which of the provided exit codes will
        /// be returned from RunConsole.
        /// </remarks>
        /// <param name="aExitCode"></param>
        public void Quit(int aExitCode)
        {
            iQuitChannel.NonBlockingSend(aExitCode);
        }
        public int RunConsole()
        {
            int exitCode = 0;
            Channel<string> commandChannel = new Channel<string>(1);
            Channel<string> readyChannel = new Channel<string>(1);
            Thread consoleThread = new Thread(
                () =>
                {
                    while (true)
                    {
                        string prompt = readyChannel.Receive();
                        if (prompt == null)
                        {
                            return;
                        }
                        Console.Write(prompt);
                        commandChannel.Send(Console.ReadLine());
                    }
                });
            consoleThread.IsBackground = true;
            consoleThread.Start();
            while (Running)
            {
                readyChannel.Send(Prompt);
                Channel.Select(
                    iQuitChannel.CaseReceive(aExitCode =>
                    {
                        Running = false;
                        exitCode = aExitCode;
                    }),
                    commandChannel.CaseReceive(aCommand =>
                    {
                        if (aCommand == null)
                        {
                            Running = false;
                            EndOfInput = true;
                            return;
                        }
                        iCommandProcessor.ProcessCommand(aCommand);
                    }));
            }
            readyChannel.Send(null);
            return exitCode;
        }
    }
}
