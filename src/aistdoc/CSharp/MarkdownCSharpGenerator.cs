using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;

using Mono.Cecil;

namespace aistdoc 
{
    internal enum CSharpTypeKind {
        Enum,
        Interface,
        Struct,
        Class
    }

    internal class MarkdownableTypeEqualityComparer : IEqualityComparer<MarkdownableSharpType> {
        public bool Equals(MarkdownableSharpType x, MarkdownableSharpType y) {
            return x.GetNameWithKind() == y.GetNameWithKind();
        }

        public int GetHashCode(MarkdownableSharpType obj) {
            return obj.GetNameWithKind().GetHashCode();
        }
    }

    internal class MarkdownableSharpType {

        private readonly NugetPackage _package;
        private readonly TypeDefinition _type;

        public IEnumerable<XmlDocumentComment> Comments { get;  }

        public string AssymblyName => _type.Module.Assembly.Name.Name;
        public string Namespace =>  !_type.IsNested ? _type.Namespace : _type.DeclaringType.Namespace;
        public string Name => !_type.IsNested ? _type.Name : $"{_type.DeclaringType.Name}.{_type.Name}";

        public NugetPackage Package => _package;

        public string PackageName => _package?.Name;

        public string[] TargetFrameworks { get; }

        public CSharpTypeKind Kind => _type.IsInterface 
            ? CSharpTypeKind.Interface : _type.IsEnum 
                ? CSharpTypeKind.Enum : _type.IsValueType 
                ? CSharpTypeKind.Struct : CSharpTypeKind.Class;
        public string BeautifyName => CSharpBeautifier.BeautifyType(_type);

        public MarkdownableSharpType(TypeDefinition type, IEnumerable<XmlDocumentComment> comments): this(null, type, new string[] { }, comments) {
          
        }

        public MarkdownableSharpType(NugetPackage package, TypeDefinition type, string[] targetFrameworks, IEnumerable<XmlDocumentComment> comments)
        {
            _package = package;
            _type = type;
            Comments = comments;
            TargetFrameworks = targetFrameworks;
        }

        MethodDefinition[] GetConstructors()
        {
            var methods = _type.Methods.ToList();
            return _type.Methods.Where(m => (m.IsFamily || m.IsPublic) && !m.HasObsoleteAttribute() && m.IsConstructor && !m.IsStatic)
                .ToArray();
        }

        MethodDefinition[] GetMethods() {
            var methods = _type.Methods.ToList();
            return _type.Methods.Where(m => (m.IsFamily || m.IsPublic) && !m.HasObsoleteAttribute() && !m.IsSpecialName && !m.IsStatic) 
                .ToArray();
        } 

        PropertyDefinition[] GetProperties() {
            return _type.Properties.Where(p => !p.IsSpecialName && !p.HasObsoleteAttribute())
                .Where(p => p.GetMethod != null && (p.GetMethod.IsFamily || p.GetMethod.IsPublic) && !p.GetMethod.IsStatic 
                    || p.SetMethod != null && (p.SetMethod.IsFamily || p.SetMethod.IsPublic) && !p.SetMethod.IsStatic)
                .ToArray();
        }

        FieldDefinition[] GetFields() {
            return _type.Fields.Where(f => !f.IsSpecialName && !f.HasObsoleteAttribute() && (f.IsFamily || f.IsPublic) && !f.IsStatic)
                .ToArray();
        }

        EventDefinition[] GetEvents() {
            return _type.Events.Where(e => !e.IsSpecialName && !e.HasObsoleteAttribute())
                 .Where(e => e.AddMethod != null && (e.AddMethod.IsFamily || e.AddMethod.IsPublic) && !e.AddMethod.IsStatic
                    || e.RemoveMethod != null && (e.RemoveMethod.IsFamily || e.RemoveMethod.IsPublic) && !e.RemoveMethod.IsStatic)
                .ToArray();
        }

        FieldDefinition[] GetStaticFields() {
            return _type.Fields.Where(f => !f.IsSpecialName && !f.HasObsoleteAttribute() && f.IsPublic && f.IsStatic)
                .ToArray();
        }

        PropertyDefinition[] GetStaticProperties() {
            return _type.Properties.Where(p => !p.IsSpecialName && !p.HasObsoleteAttribute())
                     .Where(p => p.GetMethod != null && p.GetMethod.IsPublic && p.GetMethod.IsStatic
                         || p.SetMethod != null && p.SetMethod.IsPublic && p.SetMethod.IsStatic)
                     .ToArray();
        }

        MethodDefinition[] GetStaticMethods() {
            return _type.Methods.Where(m => m.IsPublic && !m.HasObsoleteAttribute() && !m.IsSpecialName && m.IsStatic)
              .ToArray();
        }

        EventDefinition[] GetStaticEvents() {
            return _type.Events.Where(e => !e.IsSpecialName && !e.HasObsoleteAttribute())
               .Where(e => e.AddMethod != null && e.AddMethod.IsPublic && e.AddMethod.IsStatic
                  || e.RemoveMethod != null && e.RemoveMethod.IsPublic && e.RemoveMethod.IsStatic)
              .ToArray();
        }
        void BuildTable<T>(MarkdownBuilder mb, string label, T[] array, IEnumerable<XmlDocumentComment> docs, Func<T, string> getTypeNameFunc, Func<T, string> getFieldNameFunc, Func<T, string> getFinalNameFunc) {
            if (array.Any()) {
                mb.AppendLine("### " + label);
                mb.AppendLine();

                string[] head = (this._type.IsEnum)
                    ? new[] { "Value", "Name", "Description" }
                    : new[] { "Type", "Name", "Description" };

               // IEnumerable<T> seq = array;
                if (!_type.IsEnum) {
                    array = array.OrderBy(x => getFieldNameFunc(x)).ToArray();
                }

                var data = array.Select(item2 => {
                    var fieldName = getFieldNameFunc(item2);
                    fieldName = fieldName.Replace(".ctor", "#ctor");
                    var summary = docs.FirstOrDefault(x => x.MemberName == fieldName)?.Summary ?? "";
                    var typeName = "";
                    try {
                        typeName = getTypeNameFunc(item2);
                    }
                    catch {
                        typeName = "[Unknown type]";
                    }
                    return new[] { MarkdownBuilder.MarkdownCodeQuote(typeName), getFinalNameFunc(item2), summary };

                }); 

                mb.Table(head, data);
                mb.AppendLine();

            }
        }

        public string GetFixedGenericTypeName() {
            return CSharpBeautifier.BeautifyType(_type);
        }

        public string GetKindName() {
            return Kind.ToString();
        }

        public string GetNameWithKind() {
            return GetFixedGenericTypeName() + " "+ GetKindName().ToLower();
        }

        public string GetSummary() {
            var typeDocs = Comments;
            if (typeDocs == null) {
                return "";
            }

            var info = typeDocs.FirstOrDefault();
            if (info != null && info.ClassName == _type.ToString().Replace("/", ".")) {
                return info.Summary;
            }
            
            return "" ;
        }

        public override string ToString() {
          
            var mb = new MarkdownBuilder();

            var desc = Comments.FirstOrDefault(x => x.MemberType == MemberType.Type)?.Summary ?? "";
            if (desc != "") {
                mb.AppendLine(desc);
            }
            {
                var sb = new StringBuilder();

                var stat = (_type.IsAbstract && _type.IsSealed) ? "static " : "";
                var abst = (_type.IsAbstract && !_type.IsInterface && !_type.IsSealed) ? "abstract " : "";
                var classOrStructOrEnumOrInterface = _type.IsInterface ? "interface" : _type.IsEnum ? "enum" : _type.IsValueType ? "struct" : "class";

                sb.AppendLine($"public {stat}{abst}{classOrStructOrEnumOrInterface} {CSharpBeautifier.BeautifyType(_type, isFull: true)}");
                var impl = string.Join(", ", new[] { _type.BaseType }.Concat(_type.Interfaces.Select(x => x.InterfaceType))
                    .Where(x => x != null && x.FullName != "System.Object" && x.FullName != "System.ValueType")
                    .Select(x => CSharpBeautifier.BeautifyType(x)));
                if (impl != "") {
                    sb.AppendLine("    : " + impl);
                }

                mb.Code("csharp", sb.ToString());
            }

            mb.Append("Assembly: ");
            mb.CodeQuote($"{this.AssymblyName}.dll");
            mb.AppendLine();
            mb.AppendLine();

            if (_package != null) {
                mb.Append("Package: ");
                mb.CodeQuote(_package.Name);
                mb.AppendLine();
                mb.AppendLine();

                if (TargetFrameworks.Any()) {
                    mb.Append("Target Frameworks: ");
                    mb.Append(string.Join(", ", TargetFrameworks.Select(tfm => MarkdownBuilder.MarkdownCodeQuote(tfm))));
                    mb.AppendLine();
                    mb.AppendLine();
                }
            }

            if (_type.IsEnum) {
                var enums = _type.Fields
                    .Where(x => x.Name != "value__")
                    .Select(x => new { Name = x.Name, Value = Convert.ToInt64(x.Constant) })
                    .OrderBy(x => x.Value)
                    .ToArray();

                BuildTable(mb, "Enum", enums, Comments, x => x.Value.ToString(), x => x.Name, x => x.Name);
            }
            else {
                BuildTable(mb, "Constructors", GetConstructors(), Comments, x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Fields", GetFields(), Comments, x => CSharpBeautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Properties", GetProperties(), Comments, x => CSharpBeautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Events", GetEvents(), Comments, x => CSharpBeautifier.BeautifyType(x.EventType), x => x.Name, x => x.Name);
                BuildTable(mb, "Methods", GetMethods(), Comments, x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Fields", GetStaticFields(), Comments, x => CSharpBeautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Properties", GetStaticProperties(), Comments, x => CSharpBeautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Methods", GetStaticMethods(), Comments, x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Events", GetStaticEvents(), Comments, x => CSharpBeautifier.BeautifyType(x.EventType), x => x.Name, x => x.Name);
            }

            return mb.ToString();
        }
    }


    internal static class MarkdownCSharpGenerator {

        public static MarkdownableSharpType[] LoadFromPackage(NugetPackage package, string pattern, ILogger logger)
        {

            var result = new List<MarkdownableSharpType>();
            var groups = package.Assemblies.GroupBy(a => a.Assembly.Name.Name);
            foreach (var g in groups) {
                var dict = new Dictionary<string, (TypeDefinition Type, IEnumerable<XmlDocumentComment> Comments, List<string> Targets)>();
                foreach (var pasm in g.OrderBy(p => p.TargetFramework)) {
                    var types = pasm.Assembly.Modules.SelectMany(m => m.Types)
                                             .SelectMany(t => t.NestedTypes.Where(t => t.IsNestedPublic).Concat(new[] { t }))
                                             .Where(t => (t.IsPublic || t.IsNestedPublic) && !t.IsDelegate() && !t.HasObsoleteAttribute())
                                             .Where(t => IsRequiredNamespace(t, pattern))
                                             .ToList();
                    foreach (var type in types) {

                        if (dict.TryGetValue(type.FullName, out var typeInfo)) {
                            typeInfo.Targets.Add(pasm.TargetFramework);
                        }
                        else {
                            var fullTypeName = type.FullName.Replace("/", ".");            
                            dict.Add(type.FullName, (type, pasm.Comments?.Where(c => c.ClassName == type.ToString() || c.ClassName == fullTypeName) ?? Enumerable.Empty<XmlDocumentComment>(),
                                new List<string>() { pasm.TargetFramework }));
                        }
                    }
                }

                foreach (var typeInfo in dict.Values) {
                    result.Add(new MarkdownableSharpType(package, typeInfo.Type, typeInfo.Targets.ToArray(), typeInfo.Comments));
                }
               
            }

            return result.ToArray();

        }

        public static MarkdownableSharpType[] LoadFromAssembly(string dllPath, string pattern, ILogger logger) {
            var xmlPath = Path.Combine(Directory.GetParent(dllPath).FullName, Path.GetFileNameWithoutExtension(dllPath) + ".xml");

            XmlDocumentComment[] comments = new XmlDocumentComment[0];
            if (File.Exists(xmlPath)) {
                comments = VSDocParser.ParseXmlComment(XDocument.Parse(File.ReadAllText(xmlPath)));
            }
            var commentsLookup = comments.ToLookup(x => x.ClassName);

            try {
                var assembly = AssemblyDefinition.ReadAssembly(dllPath);
                var types = assembly.Modules.SelectMany(m => m.Types);
        
                return types
                        .Where(x => x != null)
                        .Where(x => x.IsPublic && !x.IsDelegate() && !x.HasObsoleteAttribute())
                        .Where(x => IsRequiredNamespace(x, pattern))
                        .Select(x => new MarkdownableSharpType(x, comments.Where(c => c.ClassName == x.FullName)))
                        .ToArray();
            }
            catch (Exception ex) {
                logger.LogWarning("Could not load assembly. \n" + ex.Message);
                return Array.Empty<MarkdownableSharpType>();
            }
        }

        static bool IsRequiredNamespace(TypeDefinition type, string pattern) {

            if (string.IsNullOrEmpty(pattern)) {
                return true;
            }

            if (type.Namespace != null) {
                var regex = new Regex(pattern);
                return regex.IsMatch(type.Namespace);
            }

            return false;
            
        }
    }
}
