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

            _lib.RootPath = _aistantSettings.Section?.Uri;
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

                var parentSection = new ArticleSaveModel
                {
                    SectionUri = parentSectionUrl,
                    ArticleTitle = sectionName,
                    ArticleUri = sectionUrl,
                    ArticleExcerpt = module.Comment?.ShortText,
                    ArticleBody = module.Comment?.ShortText ?? "",
                    IsSection = true
                };

                //Create section for package/namespace
                if (saver.SaveArticle(parentSection)) {
                    articleCount++;
                }

                foreach (var nspace in nspaces) {
                    articleCount += ProcessModule(nspace, saver, fullSectionUrl);
                }

                if (enums.Any()){
                    var section = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Enumerations",
                        ArticleUri = "enumerations",
                        IsSection = true
                    };

                    //Create section for package/namespace
                    if (saver.SaveArticle(section)) {
                        articleCount++;
                    }

                    //Processing Enumerations
                    foreach (var @enum in enums){
                        var itemName = @enum.BeautifulName;
                        var itemSummary = @enum.Comment?.ShortText;
                        var itemContent = BuildContent(@enum);

                        var articleSaveModel = new ArticleSaveModel
                        {
                            SectionTitle = section.ArticleTitle,
                            SectionUri = fullSectionUrl.CombineWithUri(section.ArticleUri),
                            ArticleTitle = itemName,
                            ArticleUri = itemName.MakeUriFromString(),
                            ArticleExcerpt = itemSummary,
                            ArticleBody = itemContent
                        };

                        if (saver.SaveArticle(articleSaveModel))
                            articleCount++;

                    }

                }

                if (interfaces.Any()) {
                    var section = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Interfaces",
                        ArticleUri = "interfaces",
                        IsSection = true
                    };

                    //Create section for interfaces
                    if (saver.SaveArticle(section)) {
                        articleCount++;
                    }


                    //Processing Interfaces
                    foreach (var @interface in interfaces) {
                        var itemName = @interface.BeautifulName;
                        var itemSummary = @interface.Comment?.ShortText;
                        var itemContent = BuildContent(@interface);

                        var articleSaveModel = new ArticleSaveModel
                        {
                            SectionTitle = section.ArticleTitle,
                            SectionUri = fullSectionUrl.CombineWithUri(section.ArticleUri),
                            ArticleTitle = itemName,
                            ArticleUri = itemName.MakeUriFromString(),
                            ArticleExcerpt = itemSummary,
                            ArticleBody = itemContent
                        };

                        if (saver.SaveArticle(articleSaveModel))
                            articleCount++;
                    }


                }


                if (classes.Any()) {

                    var section = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Classes",
                        ArticleUri = "classes",
                        IsSection = true
                    };

                    //Create section for interfaces
                    if (saver.SaveArticle(section)) {
                        articleCount++;
                    }

                    //Processing Classes
                    foreach (var @class in classes) {
                        var itemName = @class.BeautifulName;
                        var itemSummary = @class.Comment?.ShortText;
                        var itemContent = BuildContent(@class);

                        var articleSaveModel = new ArticleSaveModel
                        {
                            SectionTitle = section.ArticleTitle,
                            SectionUri = fullSectionUrl.CombineWithUri(section.ArticleUri),
                            ArticleTitle = itemName,
                            ArticleUri = itemName.MakeUriFromString(),
                            ArticleExcerpt = itemSummary,
                            ArticleBody = itemContent
                        };

                        if (saver.SaveArticle(articleSaveModel))
                            articleCount++;
                    }


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

            mb.AppendLine(@enum.Comment?.ShortText ?? "");
            mb.AppendLine();

            BuildExample(mb, @enum.Comment);


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

            BuildExample(mb, @class.Comment);

            BuildImplementedTypes(mb, @class);
            BuildExtendedTypes(mb, @class);

            BuildIndex(mb, @class);

            if (@class.Constructor != null) {
                mb.Header(2, "Constructors");
                mb.AppendSeparateLine();
                mb.Header(4, "constructor");
                mb.AppendLine("⊕ " + @class.Constructor.Signature.Format(_lib));
                if (!string.IsNullOrEmpty(@class.Constructor.Signature.Comment?.ShortText)) {
                    mb.AppendLine(@class.Constructor.Signature.Comment.ShortText);
                }

                if (@class.Constructor.Signature.Parameters.Count > 0) {
                    mb.Header(5, "Parameters");

                    foreach (var param in @class.Constructor.Signature.Parameters) {
                        mb.List($"{ParameterInfo(param)}{(param.Comment != null ? "- " + param.Comment.ShortText : "")}");
                    }
                    mb.AppendLine();
                }

                BuildExample(mb, @class.Constructor.Signature.Comment);

                mb.AppendSeparateLine();
            }

            mb.AppendLine();


            var publicMethods = @class.Methods.Where(m => !m.IsStatic && !m.IsPrivate && !m.IsProtected).ToList();
            var protectedMethods = @class.Methods.Where(m => m.IsProtected).ToList();
            var staticMethods = @class.Methods.Where(m => m.IsStatic && !m.IsPrivate).ToList();


            var publicProperties = @class.Properties.Where(p => !p.IsStatic && !p.IsPrivate && !p.IsProtected).ToList();
            var protectedProperties = @class.Properties.Where(p => p.IsProtected).ToList();
            var staticProperties = @class.Properties.Where(p => p.IsStatic && !p.IsPrivate).ToList();

            if (publicProperties.Any()) {
                mb.Header(2, "Public Properties");
                mb.AppendSeparateLine();
                foreach (var property in publicProperties) {
                    BuildContent(mb, property);
                }
            }

            if (protectedProperties.Any()) {
                mb.Header(2, "Protected Properties");
                mb.AppendSeparateLine();
                foreach (var property in protectedProperties) {
                    BuildContent(mb, property);
                }
            }

            if (staticProperties.Any()) {
                mb.Header(2, "Static Properties");
                mb.AppendSeparateLine();
                foreach (var property in staticProperties) {
                    BuildContent(mb, property);
                }
            }

            if (publicMethods.Any()) {
                mb.Header(2, "Public Methods");
                mb.AppendSeparateLine();
                foreach (var method in publicMethods) {
                    BuildContent(mb, method);
                }
            }

            if (protectedMethods.Any()) {
                mb.Header(2, "Protected Methods");
                mb.AppendSeparateLine();
                foreach (var method in protectedMethods) {
                    BuildContent(mb, method);
                }
            }

            if (staticMethods.Any()) {
                mb.Header(2, "Static Methods");
                mb.AppendSeparateLine();
                foreach (var method in staticMethods) {
                    BuildContent(mb, method);
                }
            }

            return mb.ToString();
        }

        private void BuildIndex(MarkdownBuilder mb, TypeScriptClass @class)
        {
            var publicMethods = @class.Methods.Where(m => !m.IsStatic && !m.IsPrivate && !m.IsProtected).ToList();
            var protectedMethods = @class.Methods.Where(m => m.IsProtected).ToList();
            var staticMethods = @class.Methods.Where(m => m.IsStatic && !m.IsPrivate).ToList();


            var publicProperties = @class.Properties.Where(p => !p.IsStatic && !p.IsPrivate && !p.IsProtected).ToList();
            var protectedProperties = @class.Properties.Where(p => p.IsProtected).ToList();
            var staticProperties = @class.Properties.Where(p => p.IsStatic && !p.IsPrivate).ToList();

            var path = @class.GetPath().MakeUriFromString();
            //Index region
            mb.Header(2, "Index");
            if (@class.Constructor != null) {
                mb.HeaderWithLink(3, "Constructors", CombineWithRootUrl(path.CombineWithUri("#constructors-1")));
                mb.ListLink("constructor", CombineWithRootUrl(path.CombineWithUri("#constructor")));

                mb.AppendLine();
            }

            if (publicProperties.Any()) {
                mb.HeaderWithLink(3, "Public Properties", CombineWithRootUrl(path.CombineWithUri("#public-properties-1")));

                foreach (var property in publicProperties) {
                    mb.ListLink(property.Name, CombineWithRootUrl(path.CombineWithUri("#" + property.Name.MakeUriFromString())));
                }

                mb.AppendLine();
            }

            if (protectedProperties.Any()) {
                mb.HeaderWithLink(3, "Protected Properties", CombineWithRootUrl(path.CombineWithUri("#protected-properties-1")));

                foreach (var property in protectedProperties) {
                    mb.ListLink(property.Name, CombineWithRootUrl(path.CombineWithUri("#" + property.Name.MakeUriFromString())));
                }

                mb.AppendLine();
            }

            if (staticProperties.Any()) {
                mb.HeaderWithLink(3, "Static Properties", CombineWithRootUrl(path.CombineWithUri("#static-properties-1")));

                foreach (var property in staticProperties) {
                    mb.ListLink(property.Name, CombineWithRootUrl(path.CombineWithUri("#" + property.Name.MakeUriFromString())));
                }

                mb.AppendLine();
            }

            if (publicMethods.Any()) {
                mb.HeaderWithLink(3, "Public Methods", CombineWithRootUrl(path.CombineWithUri("#public-methods-1")));
                foreach (var method in publicMethods) {
                    mb.ListLink(method.Name, CombineWithRootUrl(path.CombineWithUri("#" + method.Name.MakeUriFromString())));
                }
                mb.AppendLine();
            }

            if (protectedMethods.Any()) {
                mb.HeaderWithLink(3, "Protected Methods", CombineWithRootUrl(path.CombineWithUri("#protected-methods-1")));
                foreach (var method in protectedMethods) {
                    mb.ListLink(method.Name, CombineWithRootUrl(path.CombineWithUri("#" + method.Name.MakeUriFromString())));
                }
                mb.AppendLine();
            }

            if (staticMethods.Any()) {
                mb.HeaderWithLink(3, "Static Methods", CombineWithRootUrl(path.CombineWithUri("#static-methods-1")));
                foreach (var property in staticMethods) {
                    mb.ListLink(property.Name, CombineWithRootUrl(path.CombineWithUri("#" + property.Name.MakeUriFromString())));
                }
                mb.AppendLine();
            }
            mb.AppendLine();

        }

        private string BuildContent(List<TypeScriptFunction> functions)
        {
            var mb = new MarkdownBuilder();

            BuildIndex(mb, functions);

            foreach (var function in functions) {
                BuildContent(mb, function);
            }


            return mb.ToString();
        }

        private void BuildIndex(MarkdownBuilder mb, List<TypeScriptFunction> functions)
        {
            mb.Header(2, "Index");
            foreach (var function in functions) {
                mb.ListLink(function.Name, CombineWithRootUrl(function.GetPath().MakeUriFromString().CombineWithUri("#" + function.Name.MakeUriFromString())));
            }

            mb.AppendLine();
        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptFunction function)
        {
            mb.Header(4, function.Name);
            mb.AppendLine(function.Format(_lib));
            mb.AppendLine();
            if (!string.IsNullOrEmpty(function.Signature.Comment?.ShortText)) {
                mb.AppendLine(function.Signature.Comment.ShortText);
            }

            if (function.Signature.Parameters.Count > 0) {
                mb.Header(5, "Parameters");


                foreach (var param in function.Signature.Parameters) {
                    var paramInfo = ParameterInfo(param);
                    if (param.Comment?.ShortText != null) {
                        paramInfo += " - " + param.Comment.ShortText;
                    }

                    if (param.Comment?.Text != null) {
                        paramInfo += " - " + param.Comment.Text;
                    }

                    mb.List(paramInfo);
                }

                mb.AppendLine();
            }

            mb.AppendLine();
        
            mb.Append($"**Returns** " + function.Signature.Type.Format(_lib));
            if (!string.IsNullOrEmpty(function.Signature.Comment?.Returns)) {
                mb.Append(" - " + function.Signature.Comment.Returns);
            }


            mb.AppendLine();

            BuildExample(mb, function.Signature.Comment);

            mb.AppendLine();
            mb.AppendSeparateLine();
        }

        private string BuildContent(List<TypeScriptVariable> variables)
        {
            var mb = new MarkdownBuilder();

            BuildIndex(mb, variables);

            foreach (var variable in variables) {
                BuildContent(mb, variable);
            }

            return mb.ToString();
        }

        private void BuildIndex(MarkdownBuilder mb, List<TypeScriptVariable> variables)
        {
            mb.Header(2, "Index");
            foreach (var variable in variables) {
                mb.ListLink(variable.Name, CombineWithRootUrl(variable.GetPath().MakeUriFromString().CombineWithUri("#" + variable.Name.MakeUriFromString())));
            }

            mb.AppendLine();
        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptVariable variable)
        {
            mb.Header(4, MarkdownBuilder.MarkdownCodeQuote(variable.IsConst ? "const" : variable.IsLet ? "let" : "var") + " " +  variable.Name);
            mb.AppendLine(variable.Format(_lib));
            mb.AppendLine();
            if (!string.IsNullOrEmpty(variable.Comment?.ShortText)) {
                mb.AppendLine(variable.Comment.ShortText);
            }

            mb.AppendLine();

            BuildExample(mb, variable.Comment);

            mb.AppendSeparateLine();
        }

        private string BuildContent(TypeScriptInterface @interface)
        {

            var mb = new MarkdownBuilder();
            if (!string.IsNullOrEmpty(@interface.Comment?.ShortText)){
                mb.AppendLine(@interface.Comment.ShortText);
                mb.AppendLine();
            }


            BuildExample(mb, @interface.Comment);

            BuildImplementedTypes(mb, @interface);

            BuildIndex(mb, @interface);

            if (@interface.Properties.Any()) {
                mb.Header(2, "Properties");
                mb.AppendSeparateLine();
                foreach (var property in @interface.Properties) {
                    BuildContent(mb, property);
                }
            }


            if (@interface.Methods.Any()) {
                mb.Header(2, "Methods");
                mb.AppendSeparateLine();
                foreach (var method in @interface.Methods) {
                    BuildContent(mb, method);
                }
            }

            return mb.ToString();
        }

        private void BuildImplementedTypes(MarkdownBuilder mb, ITypeScriptImplemented im)
        {
            if (im.ImplementedTypes.Any()) {
                mb.Header(2, "Implements");
                mb.AppendLine(string.Join(", ", im.ImplementedTypes.Select(t => t.Format(_lib))));
                mb.AppendLine();

            }
        }

        private void BuildExtendedTypes(MarkdownBuilder mb, ITypeScriptExtended ex)
        {
            if (ex.ExtendedTypes.Any()) {
                mb.Header(2, "Extends");
                mb.AppendLine(string.Join(", ", ex.ExtendedTypes.Select(t => t.Format(_lib))));
                mb.AppendLine();
            }
        }

        private void BuildIndex(MarkdownBuilder mb, TypeScriptInterface @interface)
        {
            var path = @interface.GetPath().MakeUriFromString();

            mb.Header(2, "Index");
            if (@interface.Properties.Any()) {
                mb.HeaderWithLink(3, "Properties", CombineWithRootUrl(path.CombineWithUri("#properties-1")));

                foreach (var property in @interface.Properties) {
                    mb.ListLink(property.Name, CombineWithRootUrl(path.CombineWithUri("#" + property.Name.MakeUriFromString())));
                }

                mb.AppendLine();
            }

            if (@interface.Methods.Any()) {
                mb.HeaderWithLink(3, "Methods", CombineWithRootUrl(path.CombineWithUri("#methods-1")));
                foreach (var method in @interface.Methods) {
                    mb.ListLink(method.Name, CombineWithRootUrl(path.CombineWithUri("#" + method.Name.MakeUriFromString())));
                }
                mb.AppendLine();
            }

            mb.AppendLine();

        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptMethod method)
        {
            mb.Header(4, method.Name);
            mb.AppendLine(method.Format(_lib));
            mb.AppendLine();
            if (!string.IsNullOrEmpty(method.Signature.Comment?.ShortText)) {
                mb.AppendLine(method.Signature.Comment.ShortText);
            }

            if (method.Signature.Parameters.Count > 0) {
                mb.Header(5, "Parameters");

                foreach (var param in method.Signature.Parameters) {
                    var paramInfo = ParameterInfo(param);
                    if (param.Comment?.ShortText != null) {
                        paramInfo += " - " + param.Comment.ShortText;
                    }

                    if (param.Comment?.Text != null) {
                        paramInfo += " - " + param.Comment.Text;
                    }

                    mb.List(paramInfo);
                }

                mb.AppendLine();
            }

            mb.AppendLine();
            mb.Append($"**Returns** " + method.Signature.Type.Format(_lib));
            if (!string.IsNullOrEmpty(method.Signature.Comment?.Returns)) {
                mb.Append(" - " + method.Signature.Comment.Returns);
            }
            mb.AppendLine();

            BuildExample(mb, method.Signature.Comment);

            mb.AppendSeparateLine();
        }

        private string ParameterInfo(TypeScriptParameter param)
        {
            
            var result = param.Name + ": " + param.Type.Format(_lib);

            if (param.IsOptional) {
                result += ", " + MarkdownBuilder.MarkdownItalic("Optional") + " ";
            }
            else if (param.IsRest) {
                result += ", " + MarkdownBuilder.MarkdownItalic("Rest") + " ";
            }
            else if (param.DefaultValue != null) {
                result += ", " + MarkdownBuilder.MarkdownItalic("Default value") + " = " + MarkdownBuilder.MarkdownCodeQuote(param.DefaultValue); ;
            }

            return result; 
        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptProperty property)
        {
            mb.Header(4, property.Name);
            mb.AppendLine(property.Format(_lib));
            mb.AppendLine();
            if (!string.IsNullOrEmpty(property.Comment?.ShortText)) {
                mb.AppendLine(property.Comment.ShortText);
            }

            BuildExample(mb, property.Comment);

            mb.AppendSeparateLine();
        }

        private string CombineWithRootUrl(string url)
        {

            return _lib.RootPath != null ? _lib.RootPath.CombineWithUri(url) : url;
        }

        private void BuildExample(MarkdownBuilder mb, TypeScriptComment comment)
        {
            if (comment != null) {
                if (comment.Tags.TryGetValue("example", out var text)) {
                    mb.Header(5, "Example");
                    mb.Code("typescript", text);
                }
            }
        }
    }
}
