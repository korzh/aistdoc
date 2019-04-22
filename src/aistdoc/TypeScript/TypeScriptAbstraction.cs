using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace aistdoc
{
    public enum FormatMode
    {
        Markdown = 0
    }

    public interface ITypeScriptModule
    {
        string BeautifulName { get; }

        TypeScriptComment Comment {get; }
        bool IsExported { get; }
        List<TypeScriptClass> Classes { get; }
        List<TypeScriptInterface> Interfaces { get; }
        List<TypeScriptNamespace> Namespaces { get; }
        List<TypeScriptEnumeration> Enumerations { get; }
        List<TypeScriptFunction> Functions { get; }
        List<TypeScriptVariable> Variables { get; }
    }

    public interface ITypeScriptContract
    {
        List<TypeScriptProperty> Properties { get; set; } 
        List<TypeScriptMethod> Methods { get; set; }
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

        public override string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return string.Join("|", Types.Select(t => t.Format(lib, mode)));
        }
    }

    public class TypeScriptReflectionType : TypeScriptType
    {
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();

        public override string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return $"({string.Join(",", Signature.Parameters.Select(p => p.Format(lib, mode)))}) => {Signature.Type.Format(lib, mode)}";
        }

    }
    #endregion

    public class TypeScriptInterface: ITypeScriptContract
    {
        public string Name { get; set; }

        public string BeautifulName => Name + " interface";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
    }

    public class TypeScriptProperty: ITypeScriptFormatter
    {
        public string Name { get; set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsPublic { get; set; }
        public bool IsProtected { get; set; }
        public bool IsPrivate { get => !(IsProtected || IsPublic); }
        public bool IsOptional { get; set; }
        public bool IsStatic { get; set; }
        public TypeScriptType Type { get; set; }
        public string DefaultValue { get; set; }

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return $"● {Name}{(IsOptional ? "?" : "")}:{Type.Format(lib)}{((DefaultValue != null) ? " = " + MarkdownBuilder.MarkdownCodeQuote(DefaultValue) : "")}";
        }

    }

    public class TypeScriptMethod: ITypeScriptFormatter
    {
        public string Name { get; set; }
        public bool IsPublic { get; set; }
        public bool IsProtected { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsOptional { get; set; }
        public bool IsStatic { get; set; }
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown) {
            return "▸ " + Signature.Format(lib);
        }

    }

    public class TypeScriptClass: ITypeScriptContract
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string BeautifulName => Name + " class";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public TypeScriptMethod Constructor { get; set; }
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
       
    }

    public class TypeScriptParameter: ITypeScriptFormatter
    {
        public string Name { get; set; }

        public TypeScriptComment Comment { get; set; }
        public bool IsOptional { get; set; }

        public bool IsRest { get; set; }

        public TypeScriptType Type { get; set; }
        public string DefaultValue { get; set; }

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            var result = "";
            if (IsRest) {
                result += "...";
            }

            result += Name;

            if (DefaultValue != null) {
                result += " = " + MarkdownBuilder.MarkdownCodeQuote(DefaultValue); 
            }
            else if (IsOptional) {
                result += "?";
            }
          

            result += ": ";
            result += Type.Format(lib, mode);

            return result;
        }

    }


    /// <summary>
    /// Describes a function signature with call name, parameters and retun type
    /// </summary>
    public class TypeScriptSignature: ITypeScriptFormatter
    {
        public string Name { get; set; }
        public List<TypeScriptParameter> Parameters { get; set; } = new List<TypeScriptParameter>();
        public TypeScriptType Type { get; set; }
        public TypeScriptComment Comment { get; set; }

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown) {
            return $"{Name}({string.Join(",", Parameters.Select(p => p.Format(lib, mode)))}): {Type.Format(lib, mode)}";
        }

    }

    public class TypeScriptFunction: ITypeScriptFormatter
    {
        public string Name { get; set; }
        public string BeutifulName => Name + " function";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return "▸ " + Signature.Format(lib);
        }
     
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

    public class TypeScriptVariable: ITypeScriptFormatter
    {
        public string Name { get; set; }
        public string BeutifulName => Name + " variable";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public bool IsConst { get; set; }
        public bool IsLet { get; set; }
        public TypeScriptType Type { get; set; }
        public string DefaultValue { get; set; }

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return $"● {Name}:{Type.Format(lib)}{((DefaultValue != null) ? " = " + MarkdownBuilder.MarkdownCodeQuote(DefaultValue) : "")}";
        }
    }

    public class TypeScriptNamespace: ITypeScriptModule
    {
        public string Name { get; set; }
        public string BeautifulName => Name + " namespace";

        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public List<TypeScriptClass> Classes { get; } = new List<TypeScriptClass>();
        public List<TypeScriptInterface> Interfaces { get; } = new List<TypeScriptInterface>();
        public List<TypeScriptFunction> Functions { get; } = new List<TypeScriptFunction>();
        public List<TypeScriptEnumeration> Enumerations { get; } = new List<TypeScriptEnumeration>();
        public List<TypeScriptNamespace> Namespaces { get; } = new List<TypeScriptNamespace>();
        public List<TypeScriptVariable> Variables { get; } = new List<TypeScriptVariable>();

    }

    public class TypeScriptPackage: ITypeScriptModule
    {
        public string Name { get; set; }

        public string BeautifulName => Name + " package";

        public TypeScriptComment Comment { get; set; }

        public bool IsExported { get; } = true;

        public List<TypeScriptClass> Classes { get; } = new List<TypeScriptClass>();
        public List<TypeScriptInterface> Interfaces { get; } = new List<TypeScriptInterface>();
        public List<TypeScriptNamespace> Namespaces { get; } = new List<TypeScriptNamespace>();
        public List<TypeScriptFunction> Functions { get; } = new List<TypeScriptFunction>();
        public List<TypeScriptEnumeration> Enumerations { get; } = new List<TypeScriptEnumeration>();
        public List<TypeScriptVariable> Variables { get; } = new List<TypeScriptVariable>();

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

        public string FindPathToType(int id, string name)
        {
            throw new System.NotImplementedException();
        }
    }
}
