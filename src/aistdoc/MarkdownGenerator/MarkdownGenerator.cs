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
    internal enum TypeKind {
        Enum,
        Interface,
        Struct,
        Class
    }

    internal class MarkdownableTypeEqualityComparer : IEqualityComparer<MarkdownableType> {
        public bool Equals(MarkdownableType x, MarkdownableType y) {
            return x.GetNameWithKind() == y.GetNameWithKind();
        }

        public int GetHashCode(MarkdownableType obj) {
            return obj.GetNameWithKind().GetHashCode();
        }
    }

    internal class MarkdownableType {

        private readonly Type _type;
        public ILookup<string, XmlDocumentComment> CommentLookUp;

        public string Namespace => _type.Namespace;
        public string Name => _type.Name;
        public TypeKind Kind => _type.IsInterface ? TypeKind.Interface : _type.IsEnum ? TypeKind.Enum : _type.IsValueType ? TypeKind.Struct : TypeKind.Class;
        public string BeautifyName => Beautifier.BeautifyType(_type);

        public MarkdownableType(Type type, ILookup<string, XmlDocumentComment> commentLookup) {
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
        void BuildTable<T>(MarkdownBuilder mb, string label, T[] array, IEnumerable<XmlDocumentComment> docs, Func<T, string> type, Func<T, string> name, Func<T, string> finalName) {
            if (array.Any()) {
                mb.AppendLine("### " + label);
                mb.AppendLine();

                string[] head = (this._type.IsEnum)
                    ? new[] { "Value", "Name", "Description" }
                    : new[] { "Type", "Name", "Description" };

               // IEnumerable<T> seq = array;
                if (!this._type.IsEnum) {
                    array = array.OrderBy(x => name(x)).ToArray();
                }

                var data = array.Select(item2 => {
                    var summary = docs.FirstOrDefault(x => x.MemberName == name(item2))?.Summary ?? "";
                    return new[] { MarkdownBuilder.MarkdownCodeQuote(type(item2)), finalName(item2), summary };

                }); 

                mb.Table(head, data);
                mb.AppendLine();

            }
        }

        public string GetFixedGenericTypeName() {
            return Beautifier.BeautifyType(_type);
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

                sb.AppendLine($"public {stat}{abst}{classOrStructOrEnumOrInterface} {Beautifier.BeautifyType(_type, true)}");
                var impl = string.Join(", ", new[] { _type.BaseType }.Concat(_type.GetInterfaces()).Where(x => x != null && x != typeof(object) && x != typeof(ValueType)).Select(x => Beautifier.BeautifyType(x)));
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
                BuildTable(mb, "Fields", GetFields(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Properties", GetProperties(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Events", GetEvents(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
                BuildTable(mb, "Methods", GetMethods(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, x => Beautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Fields", GetStaticFields(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Properties", GetStaticProperties(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Methods", GetStaticMethods(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, x => Beautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Events", GetStaticEvents(), CommentLookUp[_type.FullName], x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
            }

            return mb.ToString();
        }
    }


    internal static class MarkdownGenerator {
        public static MarkdownableType[] Load(string dllPath, string pattern) {
            var xmlPath = Path.Combine(Directory.GetParent(dllPath).FullName, Path.GetFileNameWithoutExtension(dllPath) + ".xml");

            XmlDocumentComment[] comments = new XmlDocumentComment[0];
            if (File.Exists(xmlPath)) {
                comments = VSDocParser.ParseXmlComment(XDocument.Parse(File.ReadAllText(xmlPath)));
            }
            var commentsLookup = comments.ToLookup(x => x.ClassName);

            var assemblies = new List<Assembly>(new[] { Assembly.LoadFrom(dllPath) });

            var markdownableTypes = assemblies
                .SelectMany(x => {
                    try {
                        return x.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex) {
                        return ex.Types.Where(t => t != null);
                    }
                    catch {
                        return Type.EmptyTypes;
                    }
                })
                .Where(x => x != null)
                .Where(x => x.IsPublic && !typeof(Delegate).IsAssignableFrom(x) && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .Where(x => IsRequiredNamespace(x, pattern))
                .Select(x => new MarkdownableType(x, commentsLookup))
                .ToArray();


            return markdownableTypes;
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
