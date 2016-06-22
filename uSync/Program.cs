using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;

namespace uSync
{
    /// <summary>
    ///  uSync console app, for running all usyncy stuff from the command
    ///  line. 
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var baseDirectory = Path.GetDirectoryName(Environment.CurrentDirectory);

            var umbracoDomain = AppDomain.CreateDomain(
                "Umbraco",
                new Evidence(),
                new AppDomainSetup
                {
                    ApplicationBase = baseDirectory,
                    PrivateBinPath = Path.Combine(baseDirectory, "bin"),
                    ConfigurationFile = Path.Combine(baseDirectory, "web.config")
                });


            umbracoDomain.SetData("args", args);
            umbracoDomain.DoCallBack(RunUmbraco);
        }

        private static void RunUmbraco()
        {
            Console.WriteLine(@"        ____                   ");
            Console.WriteLine(@"  _   _/ ___| _   _ _ __   ___ ");
            Console.WriteLine(@" | | | \___ \| | | | '_ \ / __|");
            Console.WriteLine(@" | |_| |___) | |_| | | | | (__ ");
            Console.WriteLine(@"  \__,_|____/ \__, |_| |_|\___|");
            Console.Write(@"              |___/ ");

            var application = new ConsoleApplicationBase();
            application.Start(application, new EventArgs());

            Console.WriteLine(" [loaded] ");

            var args = AppDomain.CurrentDomain.GetData("args") as string[];

            if (args != null && args.Length > 0)
            {
                var cmd = args[0];
                Console.WriteLine("Command: {0}", cmd);
                ExecuteCommand(cmd.ToLower(), args.Skip(1).ToArray());
            }

        }

        private static void ExecuteCommand(string command, params string[] options)
        {
            uSyncCommands commands = new uSyncCommands();

            switch (command)
            {
                case "import":
                    // [folder]
                    break;
                case "export":
                    // [folder] 
                    break;
                case "create-snapshot":
                    commands.CreateSnapshot(options[0]);
                    break;
                case "import-snapshot":
                    // [folder]
                    break;
                case "import-snapshots":
                    // [snapshot folder]
                    break;
                default:
                    Console.WriteLine("Command Not recongnised :( ");
                    break;
            }
        }
    }
}
