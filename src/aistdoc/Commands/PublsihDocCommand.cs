using System;
using System.Collections.Generic;
using System.Text;

using McMaster.Extensions.CommandLineUtils;

using Aistant.KbService;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace aistdoc
{
    public class PublsihDocCommand : ICommand
    {

        public  static void Configure(CommandLineApplication command)
        {
            command.Description = "Publish documentation";
            command.HelpOption("-?|-h|--help");

            var configOp = command.Option("--config:<filename> | -c:<filename>", "Config file name", optionType: CommandOptionType.SingleOrNoValue);
            var outputOp = command.Option("--output:<folder> | -c: <folder>", "Output path", optionType: CommandOptionType.SingleOrNoValue);
                                
            Func<int> runCommandFunc = new PublsihDocCommand(configOp, outputOp).Run;
            command.OnExecute(runCommandFunc);
        }

        private readonly CommandOption _configOp;

        private readonly CommandOption _outputOp;

        public PublsihDocCommand(CommandOption configOp, CommandOption outputOp)
        {
            _configOp = configOp;
            _outputOp = outputOp;
        }

        public string ConfigPath => _configOp.HasValue()
                                        ? _configOp.Value()
                                        : "aistdoc.json";

        public int Run()
        {

            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.AddConsole()
                .CreateLogger("AistDoc");

            var builder = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory());

            Console.WriteLine($"Reading config: {ConfigPath} ...");
            try {
                builder.AddJsonFile(ConfigPath);
            }
            catch (FileNotFoundException ex) {
                throw ex;
            }


            try
            {
                var startTime = DateTime.UtcNow;
                var configuration = builder.Build();

                var aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();

                IArticleSaver saver = null;
                if (_outputOp.HasValue()) {
                    saver = new FileSaver(_outputOp.Value(), logger);
                }
                else {
                    saver = new AistantSaver(aistantSettings, logger);
                }

                var mode = configuration["source:mode"].ToString();

                IDocGenerator generator;
                if (mode == "typescript") {
                    generator = new TypeScriptDocGenerator(configuration);
                }
                else {
                    generator = new CSharpDocGenerator(configuration, logger, _outputOp.Value());
                }

                var articleCount = generator.Generate(saver);

                logger.LogInformation("Done! " + $"{articleCount} documents added or updated");
                logger.LogInformation("Time Elapsed : " + (DateTime.UtcNow - startTime));
                Thread.Sleep(100);
            }
            catch (Exception ex) {
                logger.LogCritical(ex.Message);

                Thread.Sleep(100);

                return -1;
            }
          

            return 0;
        }
    }
}
