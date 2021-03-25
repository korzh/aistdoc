using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Aistant.KbService;


namespace aistdoc
{
    internal class CSharpDocGenerator : IDocGenerator
    {
        private readonly string _fileRegexPattern;
        private readonly string _nameSpaceRegexPattern;
        private readonly string _outputPath;
        private readonly AistantSettings _aistantSettings;
        private readonly List<MarkdownableSharpType> _types = new List<MarkdownableSharpType>();
        private readonly ILogger _logger;

        private readonly string _srcPath;


        public CSharpDocGenerator(IConfiguration configuration, ILogger logger, string outputPath = null)
        {
            _outputPath = outputPath;
            _aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();

            _srcPath = Path.GetFullPath(configuration.GetSection("source:path").Get<string>());

            _fileRegexPattern = configuration.GetSection("source:filter:assembly").Get<string>();

            _nameSpaceRegexPattern = configuration.GetSection("source:filter:namespace").Get<string>();

            _logger = logger;
        }


        private void LoadLibraryTypes()
        {
            Regex fileRegex = (!string.IsNullOrEmpty(_fileRegexPattern)) 
                    ? new Regex(_fileRegexPattern) 
                    : null;

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

            var assemblyFiles = Directory.GetFiles(_srcPath).Where(isFileToProcess).ToList();

            foreach (var assemblyFilePath in assemblyFiles) {
                _logger.LogInformation($"Loading assembly {assemblyFilePath}...");
                _types.AddRange(MarkdownCSharpGenerator.Load(assemblyFilePath, _nameSpaceRegexPattern, _logger));
            }
        }

        public int Generate(IArticleSaver saver)
        {
            _logger?.LogInformation($"Processing assemblies in {_srcPath}...");
            LoadLibraryTypes();

            var dest = Directory.GetCurrentDirectory();
            int articleCount = 0;

            foreach (var asmG in _types.GroupBy(x => x.AssymblyName).OrderBy(x => x.Key))
            {
                var asmSectionName = asmG.Key;
                var asmSection = new ArticleSaveModel
                {
                    ArticleTitle = asmSectionName,
                    ArticleUri = asmSectionName.MakeUriFromString(),
                    IsSection = true
                };

                if (saver.SaveArticle(asmSection)) {
                    articleCount++;
                };

                foreach (var namespaceG in asmG.GroupBy(x => x.Namespace).OrderBy(x => x.Key))
                {

                    var namespaceSectionName = namespaceG.Key + " namespace";
                    var namespaceSection = new ArticleSaveModel
                    {
                        SectionUri = asmSection.ArticleUri,
                        ArticleTitle = namespaceSectionName,
                        ArticleUri = namespaceSectionName.MakeUriFromString(),
                        IsSection = true
                    };

                    if (saver.SaveArticle(namespaceSection))
                    {
                        articleCount++;
                    };


                    foreach (var item in namespaceG.OrderBy(x => x.Name).Distinct(new MarkdownableTypeEqualityComparer()))
                    {

                        SetLinks(item, _types, _aistantSettings.Kb, _aistantSettings.Section.Uri, _aistantSettings.Team);

                        string itemName = item.GetNameWithKind();

                        string itemString = item.ToString();
                        string itemSummary = item.GetSummary();

                        bool ok = saver.SaveArticle(new ArticleSaveModel
                        {
                            SectionUri = asmSection.ArticleUri.CombineWithUri(namespaceSection.ArticleUri),
                            ArticleTitle = itemName,
                            ArticleUri = itemName.MakeUriFromString(),
                            ArticleBody = itemString,
                            ArticleExcerpt = itemSummary
                        });

                        if (ok) {
                            articleCount++;
                        }
                    }
                }
            }

            return articleCount;
        }

        private void SetLinks(MarkdownableSharpType type, List<MarkdownableSharpType> types, string kbUrl, string sectionUrl, string moniker)
        {
            foreach (var comments in type.CommentLookUp) {
                foreach (var comment in comments) {
                    comment.Summary = Regex.Replace(comment.Summary, @"<see cref=""\w:([^\""]*)""\s*\/>", m => ResolveSeeElement(m, types, kbUrl, sectionUrl, moniker));
                }
            }
        }

        private string ResolveSeeElement(Match m, List<MarkdownableSharpType> types, string kbUrl, string sectionUrl, string moniker)
        {
            var typeFullName = m.Groups[1].Value;

            var lastIndexOfPoint = typeFullName.LastIndexOf(".");
            if (lastIndexOfPoint == -1)
                return $"`{typeFullName.Replace('`', '\'')}`";

            var nameSpace = typeFullName.Remove(typeFullName.LastIndexOf("."));
            var typeName = typeFullName.Substring(typeFullName.LastIndexOf(".") + 1);

            var type = types.FirstOrDefault(t => t.Namespace == nameSpace && t.Name == typeName);
            var asmName = type?.AssymblyName ?? "";
            var foundTypeNameWithKind = type?.GetNameWithKind();
            while (string.IsNullOrEmpty(foundTypeNameWithKind)) {

                lastIndexOfPoint = nameSpace.LastIndexOf(".");

                if (lastIndexOfPoint == -1)
                    break;

                typeName = nameSpace.Substring(lastIndexOfPoint + 1);
                nameSpace = nameSpace.Remove(lastIndexOfPoint);

                type = types.FirstOrDefault(t => t.Namespace == nameSpace && t.Name == typeName);
                asmName = type?.AssymblyName ?? "";
                foundTypeNameWithKind = type?.GetNameWithKind();
            }
            if (string.IsNullOrEmpty(foundTypeNameWithKind)) {
                return $"`{typeFullName.Replace('`', '\'')}`";
            }
            string url = asmName.MakeUriFromString().CombineWithUri((nameSpace + " namespace").MakeUriFromString().CombineWithUri(foundTypeNameWithKind.MakeUriFromString()));
            if (string.IsNullOrEmpty(_outputPath)) {
                if (!string.IsNullOrEmpty(sectionUrl)) {
                    url = sectionUrl.CombineWithUri(url);
                }
            }

            return $"[{typeFullName}]({url})";
        }

    }
}
