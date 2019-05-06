﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

using Aistant.KbService;

namespace aistdoc
{
    internal class CSharpDocGenerator : IDocGenerator
    {

        private readonly string _nameSpaceRegex;
        private readonly string _outputPath;
        private readonly AistantSettings _aistantSettings;
        private readonly List<MarkdownableSharpType> _types = new List<MarkdownableSharpType>();
        public CSharpDocGenerator(IConfiguration configuration, string outputPath = null) {
            _outputPath = outputPath;
            _aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();
            var path = configuration.GetSection("source:path").Get<string>();

            var fileRegexPattern = configuration.GetSection("source:filter:assembly").Get<string>();
            Regex fileRegex = null;
            if (!string.IsNullOrEmpty(fileRegexPattern))
            {
                fileRegex = new Regex(fileRegexPattern);
            }

            _nameSpaceRegex = configuration.GetSection("source:filter:namespace").Get<string>();

            //Finds all dll files with current pattern
            Func<string, bool> isFileToProcess = (s) => {

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

            var files = Directory.GetFiles(path).Where(isFileToProcess).ToList();
            foreach (var file in files)
            {
                _types.AddRange(MarkdownCSharpGenerator.Load(file, _nameSpaceRegex));
            }
        }

        public int Generate(IArticleSaver saver)
        {
            var dest = Directory.GetCurrentDirectory();
            int articleCount = 0;
            foreach (var g in _types.GroupBy(x => x.Namespace).OrderBy(x => x.Key))
            {
                string sectionName = g.Key + " namespace";

                foreach (var item in g.OrderBy(x => x.Name).Distinct(new MarkdownableTypeEqualityComparer()))
                {

                    SetLinks(item, _types, _aistantSettings.Kb, _aistantSettings.Section.Uri, _aistantSettings.Team);

                    string itemName = item.GetNameWithKind();

                    string itemString = item.ToString();
                    string itemSummary = item.GetSummary();


                    bool ok = saver.SaveArticle(new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = sectionName.MakeUriFromString(),
                        ArticleTitle = itemName,
                        ArticleUri = itemName.MakeUriFromString(),
                        ArticleBody = itemString,
                        ArticleExcerpt = itemSummary
                    });

                    if (ok)
                    {
                        articleCount++;
                    }
                }

                articleCount++;
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
            var type = m.Groups[1].Value;

            var lastIndexOfPoint = type.LastIndexOf(".");
            if (lastIndexOfPoint == -1)
                return $"`{type.Replace('`', '\'')}`";

            var nameSpace = type.Remove(type.LastIndexOf("."));
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

    }
}