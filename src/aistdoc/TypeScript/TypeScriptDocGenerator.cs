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

        private ITypeScriptContract GetClassOrInterface(string name) 
        {
            foreach (var package in _lib.Packages) {
                foreach (var @interface in package.Interfaces) {
                    if (@interface.Name.Equals(name)) {
                        return @interface;
                    }
                }
                foreach (var @class in package.Classes) {
                    if (@class.Name.Equals(name)) {
                        return @class;
                    }
                }
            }

            return null;
        }

        private bool ContainsClassOrInterface(string name) 
        {
            return GetClassOrInterface(name) != null;
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

            //support extention interfaces
            var extensionInterfaces = module.Namespaces.Where(n => _lib.Packages.Any(p => n.Name.Contains(p.Name)))
                                                       .SelectMany(n => n.Interfaces)
                                                       .ToList();

            extensionInterfaces = extensionInterfaces.Where(i => ContainsClassOrInterface(i.Name))
                                                     .ToList();

            if (enums.Any() || classes.Any() || interfaces.Any() || functions.Any() || variables.Any() || extensionInterfaces.Any())
            {

                var parentSection = new ArticleSaveModel
                {
                    SectionUri = parentSectionUrl,
                    ArticleTitle = sectionName,
                    ArticleUri = sectionUrl,
                    ArticleExcerpt = module.Comment?.ShortText ?? " ",
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
                        var itemSummary = GetSummary(@interface, extension: false, articleUrl: null);
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
                        var itemSummary = GetSummary(@class);

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

                if (extensionInterfaces.Any()) {

                    var section = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Extensions",
                        ArticleUri = "extensions",
                        IsSection = true
                    };

                    //Create section for extensions
                    if (saver.SaveArticle(section)) {
                        articleCount++;
                    }


                    //Processing extensions
                    foreach (var extensionInterface in extensionInterfaces)
                    {
                        var itemName = extensionInterface.Name + " extensions";
                        var articleUrl = section.ArticleUri.CombineWithUri(itemName.MakeUriFromString());

                        var itemSummary = GetSummary(extensionInterface, extension: true, articleUrl: articleUrl);
                        
                        var itemContent = BuildContent(extensionInterface, extension: true, articleUrl: articleUrl);

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

                    var itemSummary = GetSummary(module, module.Functions);
               
                    var articleSaveModel = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Functions",
                        ArticleUri = "functions",
                        ArticleBody = BuildContent(functions),
                        ArticleExcerpt = itemSummary
                    };

                    if (saver.SaveArticle(articleSaveModel))
                        articleCount++;
                }

                if (variables.Any()) {

                    var itemSummary = GetSummary(module, module.Variables);

                    var articleSaveModel = new ArticleSaveModel
                    {
                        SectionTitle = sectionName,
                        SectionUri = fullSectionUrl,
                        ArticleTitle = "Variables",
                        ArticleUri = "Variables",
                        ArticleBody = BuildContent(variables),
                        ArticleExcerpt = itemSummary
                    };

                    if (saver.SaveArticle(articleSaveModel))
                        articleCount++;
                }
            }
         
            return articleCount;
        }

        private string GetSummary(TypeScriptInterface @interface, bool extension, string articleUrl) 
        {
            var itemSummary = @interface.Comment?.ShortText;
            if (string.IsNullOrEmpty(itemSummary)) {
                var properties = @interface.GetSignificantProperties();
                var methods = @interface.GetSignificantMethods();

                if (!properties.Any() && !methods.Any())  {
                    properties = @interface.Properties.Where(p => !p.IsPrivate).Take(5);
                    methods = @interface.Methods.Where(m => !m.IsPrivate).Take(5);
                }

                if (properties.Any() || methods.Any()) {
                    var path = (!extension) 
                        ? @interface.GetPath().MakeUriFromString()
                        : string.IsNullOrEmpty(articleUrl)
                            ? @interface.Module.Module.GetPath().MakeUriFromString()
                            : @interface.Module.Module.GetPath().MakeUriFromString().CombineWithUri(articleUrl);
                   
                    itemSummary = BuildHTMLList(path, properties, methods);
                }

                if (string.IsNullOrEmpty(itemSummary)) {
                    //prevent autoexcerpt
                    itemSummary = " ";
                }
            }

            return itemSummary;
        }

        private string GetSummary(TypeScriptClass @class)
        {
            var itemSummary = @class.Comment?.ShortText;
            if (string.IsNullOrEmpty(itemSummary)) {
                var properties = @class.GetSignificantProperties();
                var methods = @class.GetSignificantMethods();

                if (!properties.Any() && !methods.Any()) {
                    properties = @class.Properties.Where(p => !p.IsPrivate).Take(5);
                    methods = @class.Methods.Where(m => !m.IsPrivate).Take(5);
                }

                if (properties.Any() || methods.Any()) {
                    itemSummary = BuildHTMLList(@class.GetPath().MakeUriFromString(), properties, methods);
                }

                if (string.IsNullOrEmpty(itemSummary)) {
                    //prevent autoexcerpt
                    itemSummary = " ";
                }
            }

            return itemSummary;
        }

        private string GetSummary(ITypeScriptModule module, FunctionStore funcStore)
        {
            //prevent autoexcerpt
            var itemSummary = " ";

            var functions = funcStore.GetSignificantFunctions();
            if (!functions.Any()) {
                functions = funcStore.Where(f => f.IsExported == true).Take(10).ToList();
            }

            if (functions.Any()) {
                itemSummary = BuildHTMLList(module.GetPath().MakeUriFromString().CombineWithUri("functions"), functions);
            }

            return itemSummary;
        }

        private string GetSummary(ITypeScriptModule module, VariableStore varStore)
        {
            //prevent autoexcerpt
            var itemSummary = " ";

            var varibales = varStore.GetSignificantVariables();
            if (!varibales.Any()) {
                varibales = varStore.Where(v => v.IsExported).Take(10);
            }

            if (varibales.Any())
            {
                itemSummary = BuildHTMLList(module.GetPath().MakeUriFromString().CombineWithUri("variables"), varibales);
            }

            return itemSummary;
        }

        private string BuildHTMLList(string baseUrl, IEnumerable<TypeScriptProperty> properties, IEnumerable<TypeScriptMethod> methods)
        {
           
            var sb = new StringBuilder();
            sb.AppendLine("<ul>");

            foreach (var property in properties) {

                sb.AppendLine("<li>");

                sb.AppendFormat("<a href='{0}'>", CombineWithRootUrl(baseUrl.CombineWithUri("#" + property.Name.MakeUriFromString())));
                sb.Append(property.Name);
                sb.Append("</a>");

                if (property.Comment != null) {
                    sb.Append(" - ");
                    sb.Append(property.Comment.ShortText);
                }

                sb.AppendLine();
                sb.AppendLine("</li>");
            }

            foreach (var method in methods) {
                var signature = method.Signatures.First();

                sb.AppendLine("<li>");

                sb.AppendFormat("<a href='{0}'>", CombineWithRootUrl(baseUrl.CombineWithUri("#" + method.Name.MakeUriFromString())));
                sb.Append(method.Name);
                sb.Append("</a>");

                if (signature.Comment != null) {
                    sb.Append(" - ");
                    sb.Append(signature.Comment.ShortText);
                }

                sb.AppendLine();
                sb.AppendLine("</li>");
            }

            sb.AppendLine("</ul>");

            return sb.ToString();
        }

        private string BuildHTMLList(string baseUrl, IEnumerable<TypeScriptVariable> variables)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ul>");

            foreach (var variable in variables) { 

                sb.AppendLine("<li>");

                sb.AppendFormat("<a href='{0}'>", CombineWithRootUrl(baseUrl.CombineWithUri("#" + variable.Name.MakeUriFromString())));
                sb.Append(variable.Name);
                sb.Append("</a>");

                if (variable.Comment != null) {
                    sb.Append(" - ");
                    sb.Append(variable.Comment.ShortText);
                }

                sb.AppendLine("</li>");
            }

            sb.AppendLine("</ul>");

            return sb.ToString();
        }

        private string BuildHTMLList(string baseUrl, IEnumerable<TypeScriptFunction> functions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ul>");

            foreach (var function in functions) {
                var signature = function.Signatures.First();

                sb.AppendLine("<li>");

                sb.AppendFormat("<a href='{0}'>", CombineWithRootUrl(baseUrl.CombineWithUri("#" + function.Name.MakeUriFromString())));
                sb.Append(function.Name);
                sb.Append("</a>");

                if (signature.Comment != null) {
                    sb.Append(" - ");
                    sb.Append(signature.Comment.ShortText);
                }

                sb.AppendLine("</li>");
            }

            sb.AppendLine("</ul>");

            return sb.ToString();
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
                mb.AppendLine();

                if (!string.IsNullOrEmpty(@class.Constructor.Signatures.First().Comment?.ShortText)) {
                    mb.AppendLine(@class.Constructor.Signatures.First().Comment.ShortText);
                }

                foreach (var signature in @class.Constructor.Signatures) {
                    mb.AppendLine("⊕ " + signature.Format(_lib));
                    mb.AppendLine();
                }

    
                BuildParameters(mb, @class.Constructor.Signatures.Last().Parameters);

                BuildExample(mb, @class.Constructor.Signatures.First().Comment);

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
            mb.AppendLine();
            if (!string.IsNullOrEmpty(function.Signatures.First().Comment?.ShortText)) {
                mb.AppendLine(function.Signatures.First().Comment.ShortText);
                mb.AppendLine();
            }

            foreach (var signature in function.Signatures) {
                mb.AppendLine("▸ " + signature.Format(_lib));

                mb.AppendLine();

                BuildParameters(mb, signature.Parameters);

                mb.AppendLine();

                mb.Append($"**Returns** " + signature.Type.Format(_lib));
                if (!string.IsNullOrEmpty(signature.Comment?.Returns)) {
                    mb.Append(" - " + signature.Comment.Returns);
                }

                mb.AppendLine();
                mb.AppendLine();

            }

            BuildExample(mb, function.Signatures.First().Comment);

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
            mb.Header(3, MarkdownBuilder.MarkdownCodeQuote(variable.IsConst ? "const" : variable.IsLet ? "let" : "var") + " " +  variable.Name);
            mb.AppendLine();
            if (!string.IsNullOrEmpty(variable.Comment?.ShortText)) {
                mb.AppendLine(variable.Comment.ShortText);
                mb.AppendLine();
            }

            mb.AppendLine(variable.Format(_lib));
            mb.AppendLine();

            BuildExample(mb, variable.Comment);

            mb.AppendSeparateLine();
        }

        private string BuildContent(TypeScriptInterface @interface, bool extension = false, string articleUrl = null)
        {

            var mb = new MarkdownBuilder();
            if (!string.IsNullOrEmpty(@interface.Comment?.ShortText)){
                mb.AppendLine(@interface.Comment.ShortText);
                mb.AppendLine();
            }

            BuildExample(mb, @interface.Comment);

            if (!extension)
                BuildExtendedTypes(mb, @interface);

            BuildIndex(mb, @interface, extension, articleUrl);

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

        private void BuildIndex(MarkdownBuilder mb, TypeScriptInterface @interface, bool extension, string articleUrl)
        {
            var path = @interface.GetPath().MakeUriFromString();
            if (extension) {
                path = @interface.Module.Module.GetPath().MakeUriFromString();
                if (!string.IsNullOrEmpty(articleUrl)) {
                    path = path.CombineWithUri(articleUrl);
                }
            }

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
            mb.AppendLine();
        }

        private void BuildContent(MarkdownBuilder mb, TypeScriptMethod method)
        {
            mb.Header(3, method.Name);

            if (!string.IsNullOrEmpty(method.Signatures.First().Comment?.ShortText)) {
                mb.AppendLine(method.Signatures.First().Comment.ShortText);
                mb.AppendLine();
            }

            foreach (var signature in method.Signatures) {
                mb.AppendLine("▸ " + signature.Format(_lib));

                mb.AppendLine();

                BuildParameters(mb, signature.Parameters);

                mb.AppendLine();

                mb.Append($"**Returns** " + signature.Type.Format(_lib));
                if (!string.IsNullOrEmpty(signature.Comment?.Returns)) {
                    mb.Append(" - " + signature.Comment.Returns);
                }

                mb.AppendLine();
                mb.AppendLine();
            }

            BuildExample(mb, method.Signatures.First().Comment);

            mb.AppendSeparateLine();
        }

        private void BuildParameters(MarkdownBuilder mb, List<TypeScriptParameter> parameters) {
            if (parameters.Count > 0) {
                mb.Header(4, "Parameters:");

                foreach (var param in parameters)
                {
                    var paramInfo = ParameterInfo(param);
                    if (param.Comment != null) {
                        if (!string.IsNullOrEmpty(param.Comment.ShortText)) {
                            paramInfo += " - " + param.Comment.ShortText;
                        }
                        else if (!string.IsNullOrEmpty(param.Comment.Text)) {
                            paramInfo += " - " + param.Comment.Text;

                        }
                    }

                    mb.List(paramInfo);
                }

                mb.AppendLine();
            }

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
            mb.Header(3, property.Name);

            mb.AppendLine();
            if (!string.IsNullOrEmpty(property.Comment?.ShortText)) {
                mb.AppendLine(property.Comment.ShortText);
                mb.AppendLine();
            }

            mb.AppendLine(property.Format(_lib));

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
                    mb.Header(4, "Example: ");
                    mb.Code("typescript", text);
                }
            }
        }
    }
}
