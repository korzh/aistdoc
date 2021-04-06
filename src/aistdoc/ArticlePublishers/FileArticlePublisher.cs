using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

namespace aistdoc
{

    public class FileArticlePublisher : IArticlePublisher 
    {
        private readonly string _outputFolder;
        private readonly ILogger _logger;

        public FileArticlePublisher(string outputFolder, ILogger logger)
        {
            _outputFolder = outputFolder;
            _logger = logger;

            if( Directory.Exists(_outputFolder)) {
                Directory.Delete(_outputFolder, true);
                Directory.CreateDirectory(_outputFolder);
            }
        }


            try {

                var parentSection = model.SectionUri != null ? Path.Combine(_outputFolder, model.SectionUri) : _outputFolder;
                Directory.CreateDirectory(parentSection);
                if (model.IsSection) {
                    var sectionDir = Path.Combine(parentSection, model.ArticleUri);
                    Directory.CreateDirectory(sectionDir);
                    return true;
                }
        
          
        public bool PublishArticle(ArticlePublishModel model) 
        {
            try {        
                var articleDirectory = Path.Combine(_outputFolder, MakeFileNameFromTitle(model.SectionTitle));
                Directory.CreateDirectory(articleDirectory);

                var articleTitleAndExcerpt = "## " + model.ArticleTitle + "\n";
                articleTitleAndExcerpt += model.ArticleExcerpt + "\n";

                //section index file
                File.AppendAllText(Path.Combine(articleDirectory, "$index.md"), articleTitleAndExcerpt);

                //article file
                var filepath = Path.Combine(parentSection, MakeFileNameFromTitle(model.ArticleTitle)) + ".md";
                File.WriteAllText(filepath, model.ArticleBody);

                _logger.LogInformation("Article was published. Path: " + filepath);
            }
            catch (Exception ex) {
                _logger.LogError($"ERROR on publishing [{model.ArticleTitle}]:" + ex.Message);
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
