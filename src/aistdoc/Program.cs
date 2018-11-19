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
        private static string _nameSpaceRegex = "";
        private static string _outputPath = "";
        private static List<string> _files = new List<string>();

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
                    GenerateDefaultConfig("aistdoc.json");
                    throw new FileNotFoundException("Config file is required");
                }

                Console.WriteLine($"Reading config: {_configFilePath} ...");
                try {
                    builder.AddJsonFile(_configFilePath);
                }
                catch (FileNotFoundException ex) {
                    GenerateDefaultConfig(_configFilePath);
                    throw ex;
                }
            
                var configuration = builder.Build();

                var aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();
                var path = configuration.GetSection("source:path").Get<string>();

                var fileRegexPattern = configuration.GetSection("source:filter:assembly").Get<string>();
                Regex fileRegex = null;
                if (!string.IsNullOrEmpty(fileRegexPattern)) {
                    fileRegex = new Regex(fileRegexPattern);
                }

                _nameSpaceRegex = configuration.GetSection("source:filter:namespace").Get<string>();

                //Finds all dll files with current pattern
                Func<string, bool> isFileToProcess = (s) => {
 
                    if (!s.EndsWith(".dll")) {
                        return false;
                    }

                    if (fileRegex != null) {
                        var fileName = s.Substring(s.LastIndexOf("\\") + 1);
                        if (!fileRegex.IsMatch(fileName)) {
                            return false;
                        }
                    }                   

                    return true;
                };

                var files = Directory.GetFiles(path).Where(isFileToProcess).ToList();

                IArticleSaver saver = null;
                if (!string.IsNullOrEmpty(_outputPath)) {
                    saver = new FileSaver(_outputPath, logger);
                }
                else {
                    saver = new AistantSaver(aistantSettings, logger);
                }
 
                List<MarkdownableType> types = new List<MarkdownableType>();
                foreach (var file in files) {
                    types.AddRange(MarkdownGenerator.Load(file, _nameSpaceRegex));
                }

                int articleCount = 0;
                foreach (var g in types.GroupBy(x => x.Namespace).OrderBy(x => x.Key)) {

                    if (!Directory.Exists(dest)) {
                        Directory.CreateDirectory(dest);
                    }

                    string sectionName = g.Key + " namespace";

                    foreach (var item in g.OrderBy(x => x.Name).Distinct(new MarkdownableTypeEqualityComparer())) {

                        SetLinks(item, types, aistantSettings.Kb, aistantSettings.Section.Uri, aistantSettings.Team);
                      
                        string itemName = item.GetNameWithKind();

                        string itemString = item.ToString();
                        string itemSummary = item.GetSummary();

                        
                        bool ok = saver.SaveArticle(
                            sectionName,
                            sectionName.MakeUriFromString(),
                            itemName.MakeUriFromString(),
                            itemName,
                            itemString,
                            itemSummary          
                        );
                        
                        if (ok) {
                            articleCount++;
                        }                       
                    }

                    articleCount++;
                }

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

        static void SetLinks(MarkdownableType type, List<MarkdownableType> types, string kbUrl, string sectionUrl, string moniker) {
            foreach (var comments in type.CommentLookUp) {
                foreach (var comment in comments) {
                    comment.Summary = Regex.Replace(comment.Summary, @"<see cref=""\w:([^\""]*)""\s*\/>", m => ResolveSeeElement(m, types, kbUrl, sectionUrl, moniker));
                }
            }
        }

        static string ResolveSeeElement(Match m, List<MarkdownableType> types, string kbUrl, string sectionUrl, string moniker) {
            var type = m.Groups[1].Value;

            var lastIndexOfPoint = type.LastIndexOf(".");
            if (lastIndexOfPoint == -1)
                 return $"`{type.Replace('`', '\'')}`";

            var nameSpace =  type.Remove(type.LastIndexOf("."));
            var typeName = type.Substring(type.LastIndexOf(".") + 1);

            var foundTypeNameWithKind = types.FirstOrDefault(t => t.Namespace == nameSpace && t.Name == typeName)?.GetNameWithKind();
            while (string.IsNullOrEmpty(foundTypeNameWithKind)) {
             
                lastIndexOfPoint = nameSpace.LastIndexOf(".");

                if (lastIndexOfPoint == -1)
                    break;

                typeName = nameSpace.Substring(lastIndexOfPoint + 1);
                nameSpace = nameSpace.Remove(lastIndexOfPoint);

                foundTypeNameWithKind = types.FirstOrDefault(t => t.Namespace == nameSpace && t.Name == typeName)?.GetNameWithKind();
            }
            if (string.IsNullOrEmpty(foundTypeNameWithKind)) {
                return $"`{type.Replace('`', '\'')}`";
            }
            string url = (nameSpace + " namespace").MakeUriFromString().CombineWithUri(foundTypeNameWithKind.MakeUriFromString());
            if (string.IsNullOrEmpty(_outputPath)) {
                if (!string.IsNullOrEmpty(sectionUrl)) {
                    url = sectionUrl.CombineWithUri(url);
                }
            }

            return $"[{type}]({url})";
        }

        static bool ReadArgs(string[] args) {

            foreach (var arg in args) {
                if (arg.Contains("--config:")) {
                    _configFilePath = arg.Substring("--config:".Length);
                }
                else if (arg.Contains("--create:")) {
                    var configName = arg.Substring("--create:".Length);
                    GenerateDefaultConfig(configName);
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

        static void GenerateDefaultConfig(string configName) {
            if (string.IsNullOrEmpty(configName)) {
                configName = _configFilePath;
            }

            if (!configName.EndsWith(".json")) {
                configName += ".json";
            }

            string content = ResourceFiles.GetResourceAsString("Resources", "config.json");
            File.WriteAllText(configName, content);
            Console.WriteLine($"Config {configName} successfully created");

        }
    }

    class UnknownParameterException : Exception {
        public UnknownParameterException(string message):base(message) {

        }
    }
}
