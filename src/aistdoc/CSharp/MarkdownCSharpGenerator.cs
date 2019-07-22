using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        private readonly Type _type;
        public ILookup<string, XmlDocumentComment> CommentLookUp;

        public string Namespace => _type.Namespace;
        public string Name => _type.Name;
        public CSharpTypeKind Kind => _type.IsInterface ? CSharpTypeKind.Interface : _type.IsEnum ? CSharpTypeKind.Enum : _type.IsValueType ? CSharpTypeKind.Struct : CSharpTypeKind.Class;
        public string BeautifyName => CSharpBeautifier.BeautifyType(_type);

        public MarkdownableSharpType(Type type, ILookup<string, XmlDocumentComment> commentLookup) {
            this._type = type;
            this.CommentLookUp = commentLookup;
        }

        MethodInfo[] GetMethods() {
            return _type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                .ToArray();
        }

        PropertyInfo[] GetProperties() {
            return _type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetProperty | BindingFlags.SetProperty)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .Where(y => {
                    var get = y.GetGetMethod(true);
                    var set = y.GetSetMethod(true);
                    if (get != null && set != null) {
                        return !(get.IsPrivate && set.IsPrivate);
                    }
                    else if (get != null) {
                        return !get.IsPrivate;
                    }
                    else if (set != null) {
                        return !set.IsPrivate;
                    }
                    else {
                        return false;
                    }
                })
                .ToArray();
        }

        FieldInfo[] GetFields() {
            return _type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.SetField)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                .ToArray();
        }

        EventInfo[] GetEvents() {
            return _type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .ToArray();
        }

        FieldInfo[] GetStaticFields() {
            return _type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.SetField)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                .ToArray();
        }

        PropertyInfo[] GetStaticProperties() {
            return _type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetProperty | BindingFlags.SetProperty)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .Where(y => {
                    var get = y.GetGetMethod(true);
                    var set = y.GetSetMethod(true);
                    if (get != null && set != null) {
                        return !(get.IsPrivate && set.IsPrivate);
                    }
                    else if (get != null) {
                        return !get.IsPrivate;
                    }
                    else if (set != null) {
                        return !set.IsPrivate;
                    }
                    else {
                        return false;
                    }
                })
                .ToArray();
        }

        MethodInfo[] GetStaticMethods() {
            return _type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                .ToArray();
        }

        EventInfo[] GetStaticEvents() {
            return _type.GetEvents(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
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
                var impl = string.Join(", ", new[] { _type.BaseType }.Concat(_type.GetInterfaces()).Where(x => x != null && x != typeof(object) && x != typeof(ValueType)).Select(x => CSharpBeautifier.BeautifyType(x)));
                if (impl != "") {
                    sb.AppendLine("    : " + impl);
                }

                mb.Code("csharp", sb.ToString());
            }

            mb.AppendLine();

            if (_type.IsEnum) {
                var enums = Enum.GetNames(_type)
                    .Select(x => new { Name = x, Value = ((Int32)Enum.Parse(_type, x)) })
                    .OrderBy(x => x.Value)
                    .ToArray();

                BuildTable(mb, "Enum", enums, CommentLookUp[_type.FullName], x => x.Value.ToString(), x => x.Name, x => x.Name);
            }
            else {
                BuildTable(mb, "Fields", GetFields(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Properties", GetProperties(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Events", GetEvents(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
                BuildTable(mb, "Methods", GetMethods(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Fields", GetStaticFields(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Properties", GetStaticProperties(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Methods", GetStaticMethods(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.ReturnType), x => x.Name, x => CSharpBeautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Events", GetStaticEvents(), CommentLookUp[_type.FullName], x => CSharpBeautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
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
                var assembly = Assembly.LoadFrom(dllPath);

                Type[] types = Type.EmptyTypes;

                try {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex) {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                return types
                        .Where(x => x != null)
                        .Where(x => x.IsPublic && !typeof(Delegate).IsAssignableFrom(x) && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                        .Where(x => IsRequiredNamespace(x, pattern))
                        .Select(x => new MarkdownableSharpType(x, commentsLookup))
                        .ToArray();
            }
            catch (Exception ex) {
                logger.LogWarning("Could not load assembly. \n" + ex.Message);
                return Array.Empty<MarkdownableSharpType>();
            }
        }

        static bool IsRequiredNamespace(Type type, string pattern) {

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
