using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace aistdoc
{
    public class TypeScriptDocGenerator : IDocGenerator
    {
        private readonly TypeScriptLibrary _lib;
        public TypeScriptDocGenerator(IEnumerable<string> files)
        {
            var parser = new TypeDocJsonParser(files);
            _lib = parser.Parse();
        }
        public int Generate(IArticleSaver saver)
        {
            var articleCount = 0;
            foreach (var package in _lib.Packages) {
                var sectionName = package.BeutifulName;

                //Processing Enumerations
                foreach (var @enum in package.Enumerations.Where(e => e.IsExported)) {
                    var itemName = @enum.BeautifulName;
                    var itemSummary = @enum.Comment?.ShortText;
                    var itemContent = BuildContent(@enum);

                    saver.SaveArticle(new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = sectionName.MakeUriFromString(),
                        ArticleTitle = itemName,
                        ArticleUri = itemName.MakeUriFromString(),
                        ArticleExcerpt = itemSummary,
                        ArticleBody = itemContent
                    });
                    
                }

            }

            return articleCount;
        }

        private string BuildContent(TypeScriptEnumeration @enum) {
            var mb = new MarkdownBuilder();

            mb.AppendLine(@enum?.Comment.ShortText ?? "");
            mb.AppendLine();

            mb.Header(3, "Enum");

            var headers = new string[] { "Name", "Value", "Description" };
            var data = @enum.Members.Select(m => new string[] { m.Name, MarkdownBuilder.MarkdownCodeQuote(m.DefaultValue), m.Comment?.ShortText ?? "" });

            mb.Table(headers, data);

            return mb.ToString();
        }

        private string BuildContent(TypeScriptClass @class)
        {

            var mb = new MarkdownBuilder();
            mb.AppendLine(@class?.Comment.ShortText ?? "");
            mb.AppendLine();

            mb.Header(3, "Constructor");
            mb.AppendLine();

            mb.AppendLine();

            return mb.ToString();
        }

    }
}
