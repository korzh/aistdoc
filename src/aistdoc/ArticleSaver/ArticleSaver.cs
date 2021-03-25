using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using Aistant.KbService;

namespace aistdoc {
    public interface IArticleSaver {
        bool SaveArticle(ArticleSaveModel model);
    }

    internal class AistantSaver : IArticleSaver {
        private readonly AistantKbService _service;

        public AistantSaver(AistantSettings settings, ILogger logger ) {
            _service = new AistantKbService(settings, logger);
        }

        public bool SaveArticle(ArticleSaveModel model) {
            return _service.UploadArticleAsync(model.SectionUri, model.SectionTitle, model.ArticleUri, model.ArticleTitle, model.ArticleBody, model.ArticleExcerpt, model.IsSection).Result;
        }
    }

    public class FileSaver : IArticleSaver {
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


        public bool SaveArticle(ArticleSaveModel model) {

            try {

                var parentSection = model.SectionUri != null ? Path.Combine(_outputFolder, model.SectionUri) : _outputFolder;
                Directory.CreateDirectory(parentSection);
                if (model.IsSection) {
                    var sectionDir = Path.Combine(parentSection, model.ArticleUri);
                    Directory.CreateDirectory(sectionDir);
                    return true;
                }
        
          
                var articleTitleAndExcerpt = "## " + model.ArticleTitle + "\n";
                articleTitleAndExcerpt += model.ArticleExcerpt + "\n";

                //section index file
                File.AppendAllText(Path.Combine(parentSection, ".md"), model.ArticleBody);

                //article file
                var filepath = Path.Combine(parentSection, MakeFileNameFromTitle(model.ArticleTitle)) + ".md";
                File.WriteAllText(filepath, model.ArticleBody);

                _logger.LogInformation("Article was SAVED. Path: " + filepath);
            }
            catch (Exception ex) {
                _logger.LogError("Article '" + model.ArticleTitle + "' WASN'T SAVED");
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
