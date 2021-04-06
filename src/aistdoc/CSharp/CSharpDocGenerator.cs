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
        private readonly string _packagesPath;

        public CSharpDocGenerator(IConfiguration configuration, ILogger logger, string outputPath = null)
        {
            _outputPath = outputPath;
            _aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();

            _srcPath = Path.GetFullPath(configuration.GetSection("source:path").Get<string>());
            _packagesPath = Path.GetFullPath(configuration.GetSection("source:packages").Get<string>());

            _fileRegexPattern = configuration.GetSection("source:filter:assembly").Get<string>();

            _nameSpaceRegexPattern = configuration.GetSection("source:filter:namespace").Get<string>();

            _logger = logger;
        }


        private void LoadLibraryTypes()
        {
            if (_packagesPath != null)
            {
                LoadPackages();
            }
            else 
            {
                LoadAssemblies();
            }
        }

        private void LoadPackages()
        {
            Regex fileRegex = (!string.IsNullOrEmpty(_fileRegexPattern))
                  ? new Regex(_fileRegexPattern)
                  : null;

            var library = new CSharpLibrary();
            library.RootPath = _aistantSettings?.Section?.Uri ?? "";
            var packagesFiles = Directory.GetFiles(_packagesPath, "*.nupkg");
            foreach (var packageFilePath in packagesFiles) {
                _logger.LogInformation($"Loading package {packageFilePath}...");
                var package = NugetPackage.Load(packageFilePath, fileRegex);
                library.Packages.Add(package);
                _types.AddRange(MarkdownCSharpGenerator.LoadFromPackage(library, package, _nameSpaceRegexPattern, _logger));
            }
            foreach (var type in _types) {
                library.Types.TryAdd(type.ClrType.FullName, type);
            }
        }

        private void LoadAssemblies()
        {
            Regex fileRegex = (!string.IsNullOrEmpty(_fileRegexPattern))
                    ? new Regex(_fileRegexPattern)
                    : null;

            //Finds all dll files with current pattern
            Func<string, bool> isFileToProcess = (s) =>
            {

                if (!s.EndsWith(".dll"))
                {
                    return false;
                }

                if (fileRegex != null)
                {
                    var fileName = s.Substring(s.LastIndexOf("\\") + 1);
                    if (!fileRegex.IsMatch(fileName))
                    {
                        return false;
                    }
                }

                return true;
            };

            var library = new CSharpLibrary();
            library.RootPath = _aistantSettings?.Section?.Uri ?? "";

            var assemblyFiles = Directory.GetFiles(_srcPath).Where(isFileToProcess).ToList();
            foreach (var assemblyFilePath in assemblyFiles)
            {
                _logger.LogInformation($"Loading assembly {assemblyFilePath}...");
                _types.AddRange(MarkdownCSharpGenerator.LoadFromAssembly(library, assemblyFilePath, _nameSpaceRegexPattern, _logger));
            }
            foreach (var type in _types)
            {
                library.Types.TryAdd(type.ClrType.FullName, type);
            }
        }

        public int Generate(IArticlePublisher publisher)
        {
            _logger?.LogInformation($"Processing assemblies in {_srcPath}...");
            LoadLibraryTypes();

            var dest = Directory.GetCurrentDirectory();
            int articleCount = 0;

            var packageGroups = _types.GroupBy(x => x.Package).OrderBy(x => x.Key?.Name);
            foreach (var packageGroup in packageGroups) {
                var packageSectionName = packageGroup.Key?.Name;
                var packageSection = packageSectionName != null 
                    ? new ArticlePublishModel {
                            ArticleTitle = packageSectionName,
                            ArticleUri = packageSectionName.MakeUriFromString(),
                            ArticleBody = packageGroup.Key.Description,
                            ArticleExcerpt = packageGroup.Key.Description,
                            IsSection = true
                        } 
                    : null;

                if (packageSection != null && publisher.PublishArticle(packageSection)) {
                    articleCount++;
                };

                var namespaceGroups = packageGroup.GroupBy(x => x.Namespace).OrderBy(x => x.Key);

                foreach (var namespaceGroup in namespaceGroups) {
                    var namespaceSectionName = namespaceGroup.Key + " namespace";
                    var namespaceSection = new ArticlePublishModel {
                        SectionUri = packageSection?.ArticleUri,
                        ArticleTitle = namespaceSectionName,
                        ArticleUri = namespaceSectionName.MakeUriFromString(),
                        IsSection = true
                    };

                    if (publisher.PublishArticle(namespaceSection)) {
                        articleCount++;
                    };


                    var namespaceTypes = namespaceGroup.OrderBy(x => x.Name).Distinct(new MarkdownableTypeEqualityComparer());
                    foreach (var item in namespaceTypes) {

                        SetLinks(item, _types, _aistantSettings.Kb, _aistantSettings.Section.Uri, _aistantSettings.Team);

                        string itemName = item.GetNameWithKind();

                        string itemString = item.ToString();
                        string itemSummary = item.GetSummary();

                        bool ok = publisher.PublishArticle(new ArticlePublishModel {
                            SectionUri = packageSection.ArticleUri.CombineWithUri(namespaceSection.ArticleUri),
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
            foreach (var comment in type.Comments) {
                comment.Summary = Regex.Replace(comment.Summary, @"<see cref=""\w:([^\""]*)""\s*\/>", m => ResolveSeeElement(m, types, kbUrl, sectionUrl, moniker));
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
            var packageName = type?.PackageName ?? "";
            var foundTypeNameWithKind = type?.GetNameWithKind();
            while (string.IsNullOrEmpty(foundTypeNameWithKind)) {
                lastIndexOfPoint = nameSpace.LastIndexOf(".");

                if (lastIndexOfPoint == -1)
                    break;

                typeName = nameSpace.Substring(lastIndexOfPoint + 1);
                nameSpace = nameSpace.Remove(lastIndexOfPoint);

                type = types.FirstOrDefault(t => t.Namespace == nameSpace && t.Name == typeName);
                packageName = type?.PackageName ?? "";
                foundTypeNameWithKind = type?.GetNameWithKind();
            }

            if (string.IsNullOrEmpty(foundTypeNameWithKind)) {
                return $"`{typeFullName.Replace('`', '\'')}`";
            }
            string url = packageName.MakeUriFromString().CombineWithUri((nameSpace + " namespace").MakeUriFromString().CombineWithUri(foundTypeNameWithKind.MakeUriFromString()));
            if (string.IsNullOrEmpty(_outputPath)) {
                if (!string.IsNullOrEmpty(sectionUrl)) {
                    url = sectionUrl.CombineWithUri(url);
                }
            }

            return $"[{typeFullName}]({url})";
        }
    }
}
