using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

            if (t.Name.StartsWith("ValueEditorXmlSerializer"))
            { 
            
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

        public static string ToMarkdownMethodInfo(MethodDefinition methodInfo) {
            var isExtension = methodInfo.HasExtensionAttribute();

            if (methodInfo.DeclaringType.Name.StartsWith("EntityAttrXmlSerializer"))
            { 
            
            }

            var seq = methodInfo.Parameters.Select(x => {
                var suffix = x.HasDefault ? (" = " + (x.Constant ?? $"<span style='color: blue'>null</span>")) : "";
                return "`" + BeautifyType(x.ParameterType, isParam: true) + "` " + x.Name + suffix;
            });

            var beautifulMethodName = methodInfo.IsConstructor ? methodInfo.DeclaringType.Name : methodInfo.Name;
            var index = beautifulMethodName.IndexOf("`");
            if (index > 0)
                beautifulMethodName = beautifulMethodName.Remove(index);

            return beautifulMethodName + "(" + (isExtension ? "<span style='color: blue'>this</span> " : "") + string.Join(", ", seq) + ")";
        }
    }
}
