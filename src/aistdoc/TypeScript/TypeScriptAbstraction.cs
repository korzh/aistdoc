using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace aistdoc
{
    public enum FormatMode
    {
        Markdown = 0
    }

    public interface ITypeScriptLibrary
    {
        /// <summary>
        /// Finds TypeScriptTypeByItsId and Name
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <returns>The path to the type.</returns>
        string FindPathToType(int id, string name);
    }

    public interface ITypeScriptFormatter
    {
        string Format(ITypeScriptLibrary library, FormatMode mode);
    }

    #region TypeScriptTypes 

    public enum TypeScriptTokenKind
    {
        ExternalModule = 1,
        Namespace = 2,
        Enumeration = 4,
        Varialbe = 32,
        Function = 64,
        Class = 128,
        Interface = 256,
        Constructor = 512,
        Property = 1024,
        Method = 2048
    }

    public class TypeScriptType: ITypeScriptFormatter
    {
        public string Name { get; set; }
        public string Type { get; set; }

        public virtual string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return MarkdownBuilder.MarkdownCodeQuote(Name);
        }

        public static TypeScriptType CreateTypeSctiptType(string type)
        {

            switch (type)
            {
                case "union":
                    return new TypeScriptUnionType();
                case "array":
                    return new TypeScriptArrayType();
                case "reflection":
                    return new TypeScriptReflectionType();
                case "reference":
                    return new TypeScriptReferenceType();
                default:
                    return new TypeScriptType();
            }
        }

    }

    public class TypeScriptArrayType: TypeScriptType
    {
        public TypeScriptType ElementType { get; set; }

        public override string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return ElementType.Format(lib, mode) + "[]";
        }
    }

    public class TypeScriptReferenceType : TypeScriptType
    {
        public int Id { get; set; }
        public List<TypeScriptType> TypeArguments { get; set; } = new List<TypeScriptType>();

        public override string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            var name = Name;
            if (TypeArguments.Any()){
                name += "<" + string.Join(",", TypeArguments.Select(t => t.Format(lib, mode))) + ">";
            }

            return name;
        }
    }

    public class TypeScriptUnionType : TypeScriptType
    {
        public List<TypeScriptType> Types = new List<TypeScriptType>();
    }

    public class TypeScriptReflectionType : TypeScriptType
    {
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();

    }
    #endregion

    public class TypeScriptInterface
    {
        public string Name { get; set; }

        public string BeutifulName => Name + " interface";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
    }

    public class TypeScriptProperty {
        public string Name { get; set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsPublic { get; set; }
        public bool IsProteced { get; set; }
        public bool IsPrivate { get => !(IsProteced || IsPublic); }
        public bool IsOptional { get; set; }
        public TypeScriptType Type { get; set; }

    }

    public class TypeScriptMethod
    {
        public string Name { get; set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsPublic { get; set; }
        public bool IsProtected { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsOptional { get; set; }
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();

    }

    public class TypeScriptClass
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string BeutifulName => Name + " class";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public TypeScriptMethod Constructor { get; set; } = new TypeScriptMethod();
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
       
    }

    public class TypeScriptParameter
    {
        public string Name { get; set; }

        public TypeScriptComment Comment { get; set; }
        public bool IsOptional { get; set; }

        public bool IsRest { get; set; }

        public TypeScriptType Type { get; set; }
        public string DefaultValue { get; set; }

    }


    /// <summary>
    /// Describes a function signature with call name, parameters and retun type
    /// </summary>
    public class TypeScriptSignature
    {
        public string Name { get; set; }
        public List<TypeScriptParameter> Parameters { get; set; } = new List<TypeScriptParameter>();
        public TypeScriptType Type { get; set; }

    }

    public class TypeScriptFunction
    {
        public string Name { get; set; }
        public string BeutifulName => Name + " function";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();
     
    }

    public class TypeScriptEnumeration
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BeautifulName => Name + " enum";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public List<TypeScriptEnumerationMember> Members = new List<TypeScriptEnumerationMember>();
    }

    public class TypeScriptEnumerationMember
    {
        public string Name { get; set; }

        public TypeScriptComment Comment { get; set; }
        public string DefaultValue { get; set; }
    }

    public class TypeScriptVariable
    {
        public string Name { get; set; }
        public string BeutifulName => Name + " variable";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public bool IsConst { get; set; }
        public bool IsLet { get; set; }
        public TypeScriptType Type { get; set; }
        public string DefaultValue { get; set; }
    }

    public class TypeScriptNamespace
    {
        public string Name { get; set; }
        public string BeutifulName => Name + " namespace";

        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public List<TypeScriptClass> Classes = new List<TypeScriptClass>();
        public List<TypeScriptInterface> Interfaces = new List<TypeScriptInterface>();
        public List<TypeScriptFunction> Functions = new List<TypeScriptFunction>();
        public List<TypeScriptEnumeration> Enumerations = new List<TypeScriptEnumeration>();
        public List<TypeScriptNamespace> Namespaces = new List<TypeScriptNamespace>();
        public List<TypeScriptVariable> Variables = new List<TypeScriptVariable>();

    }

    public class TypeScriptPackage
    {
        public string Name { get; set; }

        public string BeutifulName => Name + " package";

        public TypeScriptComment Comment { get; set; }

        public List<TypeScriptClass> Classes = new List<TypeScriptClass>();
        public List<TypeScriptInterface> Interfaces = new List<TypeScriptInterface>();
        public List<TypeScriptNamespace> Namespaces = new List<TypeScriptNamespace>();
        public List<TypeScriptFunction> Functions = new List<TypeScriptFunction>();
        public List<TypeScriptEnumeration> Enumerations = new List<TypeScriptEnumeration>();
        public List<TypeScriptVariable> Variables = new List<TypeScriptVariable>();

    }

    public class TypeScriptComment
    {
        public string ShortText { get; set; }
        public string Text { get; set; }
        public string Returns { get; set; }
    }

    public class TypeScriptLibrary: ITypeScriptLibrary
    {
        public List<TypeScriptPackage> Packages = new List<TypeScriptPackage>();

        public string FormatType(TypeScriptType type)
        {
            if (type is TypeScriptUnionType unionType) {
                return string.Join("|", unionType.Types.Select(t => FormatType(t)));
            }
            else if (type is TypeScriptArrayType arrType) {
                return FormatType(arrType.ElementType) + "[]";
            }
            else if (type is TypeScriptReferenceType refType) {
                var name = refType.Name;
                if (refType.TypeArguments.Any()) {
                    name += "<" + string.Join(",", refType.TypeArguments.Select(t => FormatType(t))) + ">";
                }

                return name;
                //return $"[{name}]({refType.Id}:{refType.Name})";
            }
            else if (type is TypeScriptReflectionType reflectionType) {
                return $"({string.Join(",", reflectionType.Signature.Parameters.Select(p => FormatParameter(p)))}) => {FormatType(reflectionType.Signature.Type)}";
            }

            return type.Name;
        }

        public string FormatParameter(TypeScriptParameter parameter)
        {
            var result = "";
            if (parameter.Name) {

            }

            return result;
        }

    }
}
