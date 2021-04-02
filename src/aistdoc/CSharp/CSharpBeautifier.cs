using System.Linq;
using System.Text.RegularExpressions;

using Mono.Cecil;

namespace aistdoc 
{

    internal static class CSharpBeautifier {

        public static string BeautifyType(TypeReference t, bool isFull = false, bool isParam = false) {
            if (t == null) return "";
            if (t.FullName == "System.Void") return "void";
            if (t.FullName == "System.Object") return "object";
            if (t.FullName == "System.Boolean") return "bool";
            if (t.FullName == "System.String") return "string";
            if (t.FullName == "System.Int32") return "int";
            if (t.FullName == "System.Int64") return "long";
            if (t.FullName == "System.Double") return "double";

            if (!t.IsGenericInstance && !t.HasGenericParameters) {
                return (isFull) ? t.FullName.Replace("/", ".") : (t.IsNested && !isParam) ? $"{t.DeclaringType.Name}.{t.Name}" :t.Name;
            }

            string innerFormat = "";
            if (t is GenericInstanceType genType)
            {
                var args = genType.GenericArguments.ToArray();
                innerFormat = string.Join(", ", args.Select(x => BeautifyType(x, isParam: true)));
            }
            else
            {
                innerFormat = string.Join(", ", t.GenericParameters.Select(x => x.Name));
            }

            var result = Regex.Replace(isFull ? t.FullName : t.Name, @"`.+$?", "") + "<" + innerFormat + ">";
            return result;
        }

        public static string ToMarkdownTypeReference(CSharpLibrary lib, TypeReference t, bool isParam = false)
        {
            if (t == null) return "";
            if (t.FullName == "System.Void") return "`void`";
            if (t.FullName == "System.Object") return "`object`";
            if (t.FullName == "System.Boolean") return "`bool`";
            if (t.FullName == "System.String") return "`string`";
            if (t.FullName == "System.Int32") return "`int`";
            if (t.FullName == "System.Int64") return "`long`";
            if (t.FullName == "System.Double") return "`double`";

            var hasMdType = lib.Types.TryGetValue(t.FullName, out var mdType);

            string name;
            if (!t.IsGenericInstance && !t.HasGenericParameters)
            {
                name = (t.IsNested && !isParam) ? $"{t.DeclaringType.Name}.{t.Name}" : t.Name;
                if (hasMdType) {
                    return MarkdownBuilder.MarkdownUrl(name, mdType.GetPath());
                }

                return MarkdownBuilder.MarkdownCodeQuote(name);
            }

            string innerFormat = "";
            if (t is GenericInstanceType genType)
            {
                var args = genType.GenericArguments.ToArray();
                innerFormat = string.Join(", ", args.Select(x => ToMarkdownTypeReference(lib, x, isParam: true)));
            }
            else
            {
                innerFormat = string.Join(", ", t.GenericParameters.Select(x => x.Name));
            }

            name = Regex.Replace(t.Name, @"`.+$?", "");
            if (hasMdType)
            {
                name = MarkdownBuilder.MarkdownUrl(name, mdType.GetPath());
            }
            else { 
                name = MarkdownBuilder.MarkdownCodeQuote(name);
            }

            return name + "&lt;" + innerFormat + "&gt;";
        }

        public static string ToMarkdownMethodInfo(CSharpLibrary lib, MethodDefinition methodInfo) {
            var isExtension = methodInfo.HasExtensionAttribute();

            var seq = methodInfo.Parameters.Select(x => {
                var suffix = x.HasDefault ? (" = " + (x.Constant ?? $"<span style='color: blue'>null</span>")) : "";
                return ToMarkdownTypeReference(lib, x.ParameterType, isParam: true) + " "   + x.Name + suffix;
            });

            var beautifulMethodName = methodInfo.IsConstructor ? methodInfo.DeclaringType.Name : methodInfo.Name;
            var index = beautifulMethodName.IndexOf("`");
            if (index > 0)
                beautifulMethodName = beautifulMethodName.Remove(index);

            return beautifulMethodName + "(" + (isExtension ? "<span style='color: blue'>this</span> " : "") + string.Join(", ", seq) + ")";
        }
    }
}
