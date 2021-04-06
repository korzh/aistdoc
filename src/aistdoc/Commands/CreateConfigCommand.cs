using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using McMaster.Extensions.CommandLineUtils;

namespace aistdoc
{
    public class CreateConfigCommand : ICommand
    {
        public static void Configure(CommandLineApplication command)
        {
            command.Description = "Creates configuration file";
            command.HelpOption("-?|-h|--help");

            var fileNameOp = command.Option("--file:<filename> | -f:<filename>", "New config file name", optionType: CommandOptionType.SingleOrNoValue);
            var modeOp = command.Option("--mode:<mode> | -m: <mode>", "The mode. cs, ts or git", optionType: CommandOptionType.SingleOrNoValue)
                                .Accepts(b => b
                                    .Values("cs", "ts", "git")
                                );

            Func<int> runCommandFunc = new CreateConfigCommand(fileNameOp, modeOp).Run;
            command.OnExecute(runCommandFunc);

        }

        private readonly CommandOption _fileNameOp;
        private readonly CommandOption _modeOp;

        public CreateConfigCommand(CommandOption fileNameOp, CommandOption modeOp)
        {
            _fileNameOp = fileNameOp;
            _modeOp = modeOp;
        }

        public string FileName => _fileNameOp.HasValue() 
                               ? _fileNameOp.Value()
                               : "aistdoc.json";

        public string Mode => _modeOp.HasValue()
                           ? _modeOp.Value()
                           : "cs";

        public int Run()
        {
            var configName = FileName;

            if (!configName.EndsWith(".json"))
            {
                configName += ".json";
            }

            string templateFile = "config-" + ((Mode == "cs") ? "csharp" 
                                                              : (Mode == "ts") 
                                                                      ? "typescript" 
                                                                      : (Mode == "git")
                                                                            ? "git"
                                                                            : throw new UnknownParameterException("Wrong mode: " + Mode)) + ".json";

            string content = ResourceFiles.GetResourceAsString("Resources", templateFile);
            File.WriteAllText(configName, content);
            Console.WriteLine($"Config {configName} successfully created");

            return 0;
        }
    }
}
