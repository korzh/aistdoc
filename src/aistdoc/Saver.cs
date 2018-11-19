using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using Aistant.KbService;

namespace aistdoc {
    internal interface IArticleSaver {
        bool SaveArticle(string sectionTitle, string sectionUri, string articleUri, string articleTitle, string articleBody, string articleExcerpt);
    }

    internal class AistantSaver : IArticleSaver {
        private readonly AistantKbService _service;

        public AistantSaver(AistantSettings settings, ILogger logger ) {
            _service = new AistantKbService(settings, logger);
        }

        public bool SaveArticle(string sectionTitle, string sectionUri, string articleUri, string articleTitle, string articleBody, string articleExcerpt) {
            return _service.UploadArticleAsync(sectionUri, sectionTitle, articleUri, articleTitle, articleBody, articleExcerpt).Result;
        }
    }

    internal class FileSaver : IArticleSaver {
        private readonly string _outputFolder;
        private readonly ILogger _logger;

        public FileSaver(string outputFolder, ILogger logger) {
            _outputFolder = outputFolder;
            _logger = logger;

            if( Directory.Exists(_outputFolder)) {
                Directory.Delete(_outputFolder, true);
                Directory.CreateDirectory(_outputFolder);
            }
        }


        public bool SaveArticle(string sectionTitle, string sectionUri, string articleUri, string articleTitle, string articleBody, string articleExcerpt) {

            try {
        
                var articleDirectory = Path.Combine(_outputFolder, MakeFileNameFromTitle(sectionTitle));
                Directory.CreateDirectory(articleDirectory);

                var articleTitleAndExcerpt = "## " + articleTitle + "\n";
                articleTitleAndExcerpt += articleExcerpt + "\n";

                //section index file
                File.AppendAllText(Path.Combine(articleDirectory, ".md"), articleTitleAndExcerpt);

                //article file
                var filepath = Path.Combine(articleDirectory, MakeFileNameFromTitle(articleTitle)) + ".md";
                File.WriteAllText(filepath, articleBody);

                _logger.LogInformation("Article was SAVED. Path: " + filepath);
            }
            catch (Exception ex) {
                _logger.LogError("Article '" + articleTitle + "' WASN'T SAVED");
                return false;
            }
           
            return true;
        }

        private Regex _rightRegex = new Regex("[\\~#%&*{}/:<>?|\"-]");

        private string MakeFileNameFromTitle(string title) {
            return _rightRegex.Replace(title, "");
        }

    }

  
}
