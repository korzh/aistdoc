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

        public static string BeautifyType(TypeReference t, bool isFull = false) {
            if (t == null) return "";
            if (t.FullName == "System.Void") return "void";
            if (t.FullName == "System.Object") return "object";
            if (t.FullName == "System.Boolean") return "bool";
            if (t.FullName == "System.String") return "string";
            if (t.FullName == "System.Int32") return "int";
            if (t.FullName == "System.Int64") return "long";
            if (t.FullName == "System.Double") return "double";
     
            if (!t.IsGenericInstance && !t.HasGenericParameters) return (isFull) ? t.FullName : t.Name;

            string innerFormat = "";
            if (t is GenericInstanceType genType)
            {
                innerFormat = string.Join(", ", genType.GenericArguments.Select(x => BeautifyType(x)));
            }
            else
            {
                innerFormat = string.Join(", ", t.GenericParameters.Select(x => x.Name));
            }

            var result =  Regex.Replace(isFull ? t.FullName : t.Name, @"`.+$", "") + "<" + innerFormat + ">";
            return result;
        }

        public static string ToMarkdownMethodInfo(MethodDefinition methodInfo) {
            var isExtension = methodInfo.HasExtensionAttribute();

            var seq = methodInfo.Parameters.Select(x => {
                var suffix = x.HasDefault ? (" = " + (x.Constant ?? $"<span style='color: blue'>null</span>")) : "";
                return "`" + BeautifyType(x.ParameterType) + "` " + x.Name + suffix;
            });

            return (methodInfo.IsConstructor ? methodInfo.DeclaringType.Name : methodInfo.Name) + "(" + (isExtension ? "<span style='color: blue'>this</span> " : "") + string.Join(", ", seq) + ")";
        }
    }
}
