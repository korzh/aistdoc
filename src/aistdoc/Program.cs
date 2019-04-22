using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging;

using Aistant.KbService;

namespace aistdoc
{

    class Program {

        private static string _configFilePath = "aistdoc.json";
        private static string _outputPath = "";

        static void Main(string[] args) {

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Console.WriteLine($"aistdoc utility {assembly.GetName().Version.ToString()} (c) Aistant 2018");
            Console.WriteLine("Current folder: " + Directory.GetCurrentDirectory());

            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.AddConsole()
                .CreateLogger("Aistant.DocImporter");


            var startTime = DateTime.UtcNow;
            try {
                var dest = Directory.GetCurrentDirectory();

                var result = ReadArgs(args);
                if (!result)
                    return;

                var builder = new ConfigurationBuilder()
                  .SetBasePath(Directory.GetCurrentDirectory());

                if (string.IsNullOrEmpty(_configFilePath)) {
                    GenerateDefaultConfig("aistdoc.json", "cs");
                    throw new FileNotFoundException("Config file is required");
                }

                Console.WriteLine($"Reading config: {_configFilePath} ...");
                try {
                    builder.AddJsonFile(_configFilePath);
                }
                catch (FileNotFoundException ex) {
                    GenerateDefaultConfig(_configFilePath, "cs");
                    throw ex;
                }
            
                var configuration = builder.Build();

                var aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();

                IArticleSaver saver = null;
                if (!string.IsNullOrEmpty(_outputPath)) {
                    saver = new FileSaver(_outputPath, logger);
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
                    generator = new CSharpDocGenerator(configuration, _outputPath);
                }

                var articleCount = generator.Generate(saver);

                logger.LogInformation("Done! " + $"{articleCount} documents added or updated");
             
            }
            catch (Exception ex) {
                logger.LogCritical(ex.Message);
            }

            logger.LogInformation("Time Elapsed : " + (DateTime.UtcNow - startTime));
            Thread.Sleep(100);

#if DEBUG
            Console.ReadKey();
#endif

        }

        static bool ReadArgs(string[] args) {

            foreach (var arg in args) {
                if (arg.Contains("--config:")) {
                    _configFilePath = arg.Substring("--config:".Length);
                }
                else if (arg.Contains("--create:")) {
                    var configNameWithMode = arg.Substring("--create:".Length);
                    var configName = configNameWithMode.Substring(3);
                    var mode = configNameWithMode.Remove(2);
                    GenerateDefaultConfig(configName, mode);
                    return false;
                }
                else if (arg.Contains("--output:")) {
                    _outputPath = arg.Substring("--output:".Length);
                    if (string.IsNullOrEmpty(_outputPath)) {
                        _outputPath = Directory.GetCurrentDirectory();
                    } 
                }
                else if (arg == "-h" || arg == "--help") {
                    var help = ResourceFiles.GetResourceAsString("Resources", "help.txt");
                    Console.WriteLine(help);
                    return false;
                }
                else {
                    throw new UnknownParameterException(string.Format("Unknown parameter {0}\nUse: aistdoc -h to read information about existing commands", arg));
                }
            }

            return true;
        }

        static void GenerateDefaultConfig(string configName, string mode) {
            if (string.IsNullOrEmpty(configName)) {
                configName = _configFilePath;
            }

            if (!configName.EndsWith(".json")) {
                configName += ".json";
            }

            string templateFile = "config-" + ((mode == "cs") ? "csharp" : (mode == "ts") ? "typescript" : throw new UnknownParameterException("Wrong mode: " + mode)) + ".json";

            string content = ResourceFiles.GetResourceAsString("Resources", templateFile);
            File.WriteAllText(configName, content);
            Console.WriteLine($"Config {configName} successfully created");

        }
    }

    class UnknownParameterException : Exception {
        public UnknownParameterException(string message):base(message) {

        }
    }
}
