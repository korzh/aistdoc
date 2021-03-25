using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private readonly TypeDefinition _type;
        public ILookup<string, XmlDocumentComment> CommentLookUp;

        public string AssymblyName => _type.Module.Assembly.Name.Name;
        public string Namespace => _type.Namespace;
        public string Name => _type.Name;
        public CSharpTypeKind Kind => _type.IsInterface ? CSharpTypeKind.Interface : _type.IsEnum ? CSharpTypeKind.Enum : _type.IsValueType ? CSharpTypeKind.Struct : CSharpTypeKind.Class;
        public string BeautifyName => CSharpBeautifier.BeautifyType(_type);

        public MarkdownableSharpType(TypeDefinition type, ILookup<string, XmlDocumentComment> commentLookup) {
            _type = type;
            CommentLookUp = commentLookup;
        }

        MethodDefinition[] GetConstructors()
        {
            var methods = _type.Methods.ToList();
            return _type.Methods.Where(m => !m.IsPrivate && !m.HasObsoleteAttribute() && m.IsConstructor && !m.IsStatic)
                .ToArray();
        }

        MethodDefinition[] GetStaticConstructors()
        {
            var methods = _type.Methods.ToList();
            return _type.Methods.Where(m => !m.IsPrivate && !m.HasObsoleteAttribute() && m.IsConstructor && m.IsStatic)
                .ToArray();
        }

        MethodDefinition[] GetMethods() {
            var methods = _type.Methods.ToList();
            return _type.Methods.Where(m => !m.IsPrivate && !m.HasObsoleteAttribute() && !m.IsSpecialName && !m.IsStatic) 
                .ToArray();
        } 

        PropertyDefinition[] GetProperties() {
            return _type.Properties.Where(p => !p.IsSpecialName && !p.HasObsoleteAttribute())
                .Where(p => p.GetMethod != null && !p.GetMethod.IsPrivate && !p.GetMethod.IsStatic 
                    || p.SetMethod != null && !p.SetMethod.IsPrivate && !p.SetMethod.IsStatic)
                .ToArray();
        }

        FieldDefinition[] GetFields() {
            return _type.Fields.Where(f => !f.IsSpecialName && !f.HasObsoleteAttribute() && !f.IsPrivate && !f.IsStatic)
                .ToArray();
        }

        EventDefinition[] GetEvents() {
            return _type.Events.Where(e => !e.IsSpecialName && !e.HasObsoleteAttribute())
                 .Where(e => e.AddMethod != null && !e.AddMethod.IsPrivate && !e.AddMethod.IsStatic
                    || e.RemoveMethod != null && !e.RemoveMethod.IsPrivate && !e.RemoveMethod.IsStatic)
                .ToArray();
        }

        FieldDefinition[] GetStaticFields() {
            return _type.Fields.Where(f => !f.IsSpecialName && !f.HasObsoleteAttribute() && !f.IsPrivate && f.IsStatic)
                .ToArray();
        }

        PropertyDefinition[] GetStaticProperties() {
            return _type.Properties.Where(p => !p.IsSpecialName && !p.HasObsoleteAttribute())
                     .Where(p => p.GetMethod != null && !p.GetMethod.IsPrivate && p.GetMethod.IsStatic
                         || p.SetMethod != null && !p.SetMethod.IsPrivate && p.SetMethod.IsStatic)
                     .ToArray();
        }

        MethodDefinition[] GetStaticMethods() {
            return _type.Methods.Where(m => !m.IsPrivate && !m.HasObsoleteAttribute() && !m.IsSpecialName && m.IsStatic)
              .ToArray();
        }

        EventDefinition[] GetStaticEvents() {
            return _type.Events.Where(e => !e.IsSpecialName && !e.HasObsoleteAttribute())
               .Where(e => e.AddMethod != null && !e.AddMethod.IsPrivate && e.AddMethod.IsStatic
                  || e.RemoveMethod != null && !e.RemoveMethod.IsPrivate && e.RemoveMethod.IsStatic)
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
                if (!this._type.IsEnum) {
                    array = array.OrderBy(x => getFieldNameFunc(x)).ToArray();
                }

                var data = array.Select(item2 => {
                    var summary = docs.FirstOrDefault(x => x.MemberName == getFieldNameFunc(item2))?.Summary ?? "";
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
            var typeDocs = CommentLookUp[_type.ToString()];
            if (typeDocs == null) {
                return "";
            }

            var info = typeDocs.FirstOrDefault();
            if (info != null && info.ClassName == _type.ToString()) {
                return info.Summary;
            }
            
            return "" ;
        }

        public override string ToString() {
          
            var mb = new MarkdownBuilder();

            var desc = CommentLookUp[_type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Summary ?? "";
            if (desc != "") {
                mb.AppendLine(desc);
            }
            {
                var sb = new StringBuilder();

                var stat = (_type.IsAbstract && _type.IsSealed) ? "static " : "";
                var abst = (_type.IsAbstract && !_type.IsInterface && !_type.IsSealed) ? "abstract " : "";
                var classOrStructOrEnumOrInterface = _type.IsInterface ? "interface" : _type.IsEnum ? "enum" : _type.IsValueType ? "struct" : "class";

                sb.AppendLine($"public {stat}{abst}{classOrStructOrEnumOrInterface} {CSharpBeautifier.BeautifyType(_type, true)}");
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

            if (_type.IsEnum) {
                var enums = _type.Fields
                    .Where(x => x.Name != "value__")
                    .Select(x => new { Name = x.Name, Value = Convert.ToInt64(x.Constant) })
                    .OrderBy(x => x.Value)
                    .ToArray();

                BuildTable(mb, "Enum", enums, CommentLookUp[_type.FullName], x => x.Value.ToString(), x => x.Name, x => x.Name);
            }
            else {
                BuildTable(mb, "Constructors", GetConstructors(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Fields", GetFields(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Properties", GetProperties(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Events", GetEvents(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.EventType), x => x.Name, x => x.Name);
                BuildTable(mb, "Methods", GetMethods(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Constructors", GetStaticConstructors(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Fields", GetStaticFields(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Properties", GetStaticProperties(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Methods", GetStaticMethods(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Events", GetStaticEvents(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.EventType), x => x.Name, x => x.Name);
            }

            return mb.ToString();
        }
    }


    internal static class MarkdownCSharpGenerator {


        public static MarkdownableSharpType[] Load(string dllPath, string pattern, ILogger logger) {
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
                        .Select(x => new MarkdownableSharpType(x, commentsLookup))
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
