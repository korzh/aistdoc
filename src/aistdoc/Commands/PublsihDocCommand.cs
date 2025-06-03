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
            var assembliesOp = command.Option("--assemblies:<folder> | -i: <folder>", "The path to assemblies and XML files", optionType: CommandOptionType.SingleOrNoValue);
            var outputOp = command.Option("--output:<folder> | -o: <folder>", "Output path", optionType: CommandOptionType.SingleOrNoValue);
                                
            Func<int> runCommandFunc = new PublsihDocCommand(configOp, assembliesOp, outputOp).Run;
            command.OnExecute(runCommandFunc);
        }

        private readonly CommandOption _configOp;

        private readonly CommandOption _inputOp;
        private readonly CommandOption _outputOp;

        public PublsihDocCommand(CommandOption configOp, CommandOption inputOp, CommandOption outputOp)
        {
            _configOp = configOp;
            _inputOp = inputOp;
            _outputOp = outputOp;
        }

        public string ConfigPath => _configOp.HasValue()
                                        ? _configOp.Value()
                                        : "aistdoc.json";

        public int Run()
        {
            var logger = LoggerFactory.Create(b => b.AddConsole())
                .CreateLogger("AistDoc");

            try {
                var builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory());

                Console.WriteLine($"Reading config: {ConfigPath} ...");
                try {
                    builder.AddJsonFile(ConfigPath);
                }
                catch (FileNotFoundException) {
                    throw;
                }

                var startTime = DateTime.UtcNow;
                var configuration = builder.Build();

                var aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();

                IArticlePublisher publisher = null;
                if (_outputOp.HasValue()) {
                    publisher = new FileArticlePublisher(_outputOp.Value(), logger);
                }
                else {
                    publisher = new AistantArticlePublisher(aistantSettings, logger);
                }

                var mode = configuration["source:mode"]?.ToString();

                IDocGenerator generator;
                if (mode == "typescript") {
                    generator = new TypeScriptDocGenerator(configuration);
                }
                else {
                    var options = new CSharpDocGeneratorOptions();
                    options.AistantSettings = aistantSettings;
                    options.AssembliesPath = _inputOp.HasValue() ? _inputOp.Value() : configuration.GetSection("source:path").Get<string>();
                    options.PackagesPath = configuration.GetSection("source:packages").Get<string>();
                    options.OutputPath = _outputOp.Value();
                    options.FileRegexPattern = configuration.GetSection("source:filter:assembly").Get<string>();
                    options.NamespacePattern = configuration.GetSection("source:filter:namespace").Get<string>();
                    generator = new CSharpDocGenerator(options, logger);
                }

                var articleCount = generator.Generate(publisher);

                logger.LogInformation("Done! " + $"{articleCount} documents added or updated");
                logger.LogInformation("Time Elapsed : " + (DateTime.UtcNow - startTime));
                Thread.Sleep(100);
            }
            catch (Exception ex) {
                logger.LogCritical(ex.Message);
                logger.LogCritical(ex.StackTrace);
                Console.Out.WriteLine(ex.Message);
                Console.Out.WriteLine(ex.StackTrace);
                if (ex.InnerException != null) {
                    logger.LogCritical(ex.Message);
                    Console.Out.WriteLine(ex.InnerException.Message);
                }
                Thread.Sleep(100);
                return -1;
            } 

            return 0;
        }
    }
}
