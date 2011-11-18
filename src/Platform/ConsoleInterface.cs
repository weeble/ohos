using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.Os.Platform;

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
        public ConsoleInterface(ICommandProcessor aCommandProcessor)
        {
            Prompt = ">";
            Running = true;
            EndOfInput = false;
            iCommandProcessor = aCommandProcessor;
        }
        public void RunConsole()
        {
            while (Running)
            {
                Console.Write(Prompt);
                string command = Console.ReadLine();
                if (command == null)
                {
                    Running = false;
                    EndOfInput = true;
                    return;
                }
                iCommandProcessor.ProcessCommand(command);
            }
        }
    }
}
