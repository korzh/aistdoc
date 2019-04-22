using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Aistant.KbService;
using Microsoft.Extensions.Configuration;

namespace aistdoc
{
    public class TypeScriptDocGenerator : IDocGenerator
    {
        private readonly TypeScriptLibrary _lib;

        private readonly AistantSettings _aistantSettings;
        public TypeScriptDocGenerator(IConfiguration configuration)
        {
            var files = configuration.GetSection("source").GetSection("files").Get<string[]>();
            _aistantSettings = configuration.GetSection("aistant").Get<AistantSettings>();

            var parser = new TypeDocJsonParser(files);
            _lib = parser.Parse();
        }

        public int Generate(IArticleSaver saver)
        {
            var articleCount = 0;
            foreach (var package in _lib.Packages) {
                articleCount += ProcessModule(package, saver);
            }

            return articleCount;
        }

        private int ProcessModule(ITypeScriptModule module, IArticleSaver saver, string parentSectionUrl = null)
        {
            var articleCount = 0;
            var sectionName = module.BeautifulName;
            var sectionUrl = sectionName.MakeUriFromString();
            var fullSectionUrl = (parentSectionUrl != null) ? parentSectionUrl.CombineWithUri(sectionUrl) : sectionUrl;

            var enums = module.Enumerations.Where(e => e.IsExported).ToList();
            var classes = module.Classes.Where(c => c.IsExported).ToList();
            var interfaces = module.Interfaces.Where(i => i.IsExported).ToList();
            var functions = module.Functions.Where(f => f.IsExported).ToList();
            var variables = module.Variables.Where(v => v.IsExported).ToList();
            var nspaces = module.Namespaces.Where(n => n.IsExported).ToList();

            if (enums.Any() || classes.Any() || interfaces.Any() || functions.Any() || variables.Any())
            {

                var section = new ArticleSaveModel
                {
                    SectionUri = parentSectionUrl,
                    ArticleTitle = sectionName,
                    ArticleUri = sectionUrl,
                    ArticleExcerpt = module.Comment?.ShortText,
                    ArticleBody = module.Comment?.ShortText ?? "",
                    IsSection = true
                };

                //Create section for package/namespace
                if (saver.SaveArticle(section)) {
                    articleCount++;
                }

                foreach (var nspace in nspaces) {
                    articleCount += ProcessModule(nspace, saver, fullSectionUrl);
                }

                //Processing Enumerations
                foreach (var @enum in enums) {
                    var itemName = @enum.BeautifulName;
                    var itemSummary = @enum.Comment?.ShortText;
                    var itemContent = BuildContent(@enum);

                    var articleSaveModel = new ArticleSaveModel {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = itemName,
                        ArticleUri = itemName.MakeUriFromString(),
                        ArticleExcerpt = itemSummary,
                        ArticleBody = itemContent
                    };

                    if (saver.SaveArticle(articleSaveModel))
                        articleCount++;

                }

                //Processing Interfaces
                foreach (var @interface in interfaces) {
                    var itemName = @interface.BeautifulName;
                    var itemSummary = @interface.Comment?.ShortText;
                    var itemContent = BuildContent(@interface);

                    var articleSaveModel = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = itemName,
                        ArticleUri = itemName.MakeUriFromString(),
                        ArticleExcerpt = itemSummary,
                        ArticleBody = itemContent
                    };

                    if (saver.SaveArticle(articleSaveModel))
                        articleCount++;
                }

                //Processing Classes
                foreach (var @class in classes) {
                    var itemName = @class.BeautifulName;
                    var itemSummary = @class.Comment?.ShortText;
                    var itemContent = BuildContent(@class);

                    var articleSaveModel = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = itemName,
                        ArticleUri = itemName.MakeUriFromString(),
                        ArticleExcerpt = itemSummary,
                        ArticleBody = itemContent
                    };

                    if (saver.SaveArticle(articleSaveModel))
                        articleCount++;
                }

                if (functions.Any()) {
                    var articleSaveModel = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Functions",
                        ArticleUri = "functions",
                        ArticleBody = BuildContent(functions)
                    };

                    if (saver.SaveArticle(articleSaveModel))
                        articleCount++;
                }

                if (variables.Any()) {
                    var articleSaveModel = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Variables",
                        ArticleUri = "Variables",
                        ArticleBody = BuildContent(variables)
                    };

                    if (saver.SaveArticle(articleSaveModel))
                        articleCount++;
                }
            }
         
            return articleCount;
        }

        private string BuildContent(TypeScriptEnumeration @enum)
        {
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
            if (!string.IsNullOrEmpty(@class.Comment?.ShortText)) {
                mb.AppendLine(@class.Comment.ShortText);
                mb.AppendLine();
            }

            if (@class.Constructor != null) {
                mb.Header(2, "Constructors");
                mb.Header(4, "constructor");
                mb.AppendLine("⊕" + @class.Constructor.Signature.Format(_lib));
                if (!string.IsNullOrEmpty(@class.Constructor.Signature.Comment?.ShortText)) {
                    mb.AppendLine(@class.Constructor.Signature.Comment.ShortText);
                }

                if (@class.Constructor.Signature.Parameters.Count > 0) {
                    mb.Header(5, "Parameters");

                    foreach (var param in @class.Constructor.Signature.Parameters) {
                        mb.List($"{param.Format(_lib)}{(param.Comment != null ? "- " + param.Comment.ShortText : "")}");
                    }
                    mb.AppendLine();
                }
            }

            var publicMethods = @class.Methods.Where(m => m.IsPublic && !m.IsStatic).ToList();
            var protectedMethods = @class.Methods.Where(m => m.IsProtected).ToList();
            var staticMethods = @class.Methods.Where(m => m.IsStatic && !m.IsPrivate).ToList();


            var publicProperties = @class.Properties.Where(p => p.IsPublic && !p.IsStatic).ToList();
            var protectedProperties = @class.Properties.Where(p => p.IsProtected).ToList();
            var staticProperties = @class.Properties.Where(p => p.IsStatic && !p.IsPrivate).ToList();

            if (publicProperties.Any()) {
                mb.Header(2, "Public Properties");
                foreach (var property in publicProperties) {
                    BuildContent(mb, property);
                }
            }

            if (protectedProperties.Any()) {
                mb.Header(2, "Protected Properties");
                foreach (var property in protectedProperties) {
                    BuildContent(mb, property);
                }
            }

            if (staticProperties.Any()) {
                mb.Header(2, "Static Properties");
                foreach (var property in staticProperties) {
                    BuildContent(mb, property);
                }
            }

            if (publicMethods.Any()) {
                mb.Header(2, "Public Methods");
                foreach (var method in publicMethods) {
                    BuildContent(mb, method);
                }
            }

            if (protectedMethods.Any()) {
                mb.Header(2, "Protected Methods");
                foreach (var method in protectedMethods) {
                    BuildContent(mb, method);
                }
            }

            if (staticMethods.Any()) {
                mb.Header(2, "Static Methods");
                foreach (var method in staticMethods) {
                    BuildContent(mb, method);
                }
            }

            return mb.ToString();
        }

        private string BuildContent(List<TypeScriptFunction> functions)
        {
            var mb = new MarkdownBuilder();

            foreach (var function in functions) {
                BuildContent(mb, function);
            }


            return mb.ToString();
        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptFunction function)
        {
            mb.Header(4, function.Name);
            mb.AppendLine(function.Format(_lib));
            if (!string.IsNullOrEmpty(function.Signature.Comment?.ShortText)) {
                mb.AppendLine(function.Signature.Comment.ShortText);
            }

            if (function.Signature.Parameters.Count > 0) {
                mb.Header(5, "Parameters");


                foreach (var param in function.Signature.Parameters) {
                    var paramInfo = param.Format(_lib);
                    if (param.Comment?.ShortText != null) {
                        paramInfo += "- " + param.Comment.ShortText;
                    }

                    if (param.Comment?.Text != null) {
                        paramInfo += "- " + param.Comment.Text;
                    }

                    mb.List(paramInfo);
                }

                mb.AppendLine();
            }

            mb.Append($"**Returns** " + function.Signature.Type.Format(_lib));
            if (!string.IsNullOrEmpty(function.Signature.Comment?.Returns)) {
                mb.Append(" - " + function.Signature.Comment.Returns);
            }
            mb.AppendLine();
            mb.AppendLine();
            mb.AppendSeparateLine();
        }

        private string BuildContent(List<TypeScriptVariable> variables)
        {
            var mb = new MarkdownBuilder();
            foreach (var variable in variables) {
                BuildContent(mb, variable);
            }

            return mb.ToString();
        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptVariable variable)
        {
            mb.Header(4, MarkdownBuilder.MarkdownCodeQuote(variable.IsConst ? "const" : variable.IsLet ? "let" : "var") + " " +  variable.Name);
            mb.AppendLine(variable.Format(_lib));
            if (!string.IsNullOrEmpty(variable.Comment?.ShortText)) {
                mb.AppendLine(variable.Comment.ShortText);
            }

            mb.AppendLine();
            mb.AppendSeparateLine();
        }

        private string BuildContent(TypeScriptInterface @interface)
        {

            var mb = new MarkdownBuilder();
            if (!string.IsNullOrEmpty(@interface.Comment?.ShortText))
            {
                mb.AppendLine(@interface.Comment.ShortText);
                mb.AppendLine();
            }

            if (@interface.Properties.Any()) {
                mb.Header(2, "Properties");
                foreach (var method in @interface.Methods) {
                    BuildContent(mb, method);
                }
            }


            if (@interface.Methods.Any()) {
                mb.Header(2, "Methods");
                foreach (var method in @interface.Methods) {
                    BuildContent(mb, method);
                }
            }

            return mb.ToString();
        }

      
        private void BuildContent(MarkdownBuilder mb, TypeScriptMethod method)
        {
            mb.Header(4, method.Name);
            mb.AppendLine(method.Format(_lib));
            if (!string.IsNullOrEmpty(method.Signature.Comment?.ShortText)) {
                mb.AppendLine(method.Signature.Comment.ShortText);
            }

            if (method.Signature.Parameters.Count > 0) {
                mb.Header(5, "Parameters");

                foreach (var param in method.Signature.Parameters) {
                    var paramInfo = param.Format(_lib);
                    if (param.Comment?.ShortText != null) {
                        paramInfo += "- " + param.Comment.ShortText;
                    }

                    if (param.Comment?.Text != null) {
                        paramInfo += "- " + param.Comment.Text;
                    }

                    mb.List(paramInfo);
                }
                mb.AppendLine();
            }
           
            mb.Append($"**Returns** " + method.Signature.Type.Format(_lib));
            if (!string.IsNullOrEmpty(method.Signature.Comment?.Returns)) {
                mb.Append(" - " + method.Signature.Comment.Returns);
            }
            mb.AppendLine();
            mb.AppendLine();
            mb.AppendSeparateLine();
        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptProperty property)
        {
            mb.Header(4, property.Name);
            mb.AppendLine(property.Format(_lib));
            if (!string.IsNullOrEmpty(property.Comment?.ShortText)) {
                mb.AppendLine(property.Comment.ShortText);
            }

            mb.AppendLine();
            mb.AppendSeparateLine();
        }

    }
}
