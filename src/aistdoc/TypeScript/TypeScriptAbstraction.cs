using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System;

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

        string GetPath();
    }

    public interface ITypeScriptContract
    {
        List<TypeScriptProperty> Properties { get; set; } 
        List<TypeScriptMethod> Methods { get; set; }
    }

    public interface ITypeScriptImplemented
    {
        List<TypeScriptType> ImplementedTypes { get; }
    }

    public interface ITypeScriptExtended
    {
        List<TypeScriptType> ExtendedTypes { get; }
    }

    public interface ITypeScriptLibrary
    {
        /// <summary>
        /// Finds TypeScriptType by its name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The path to the type.</returns>
        string FindPathToType(string name);
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
                case "stringLiteral":
                    return new TypesScriptStringLiteralType();
                default:
                    return new TypeScriptType();
            }
        }

    }

    public class TypesScriptStringLiteralType : TypeScriptType
    {
        public string Value { get; set; }

        public override string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return MarkdownBuilder.MarkdownCodeQuote("\"" + Value + "\"");
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
            var path = lib.FindPathToType(Name);
            var name = path != null ? MarkdownBuilder.MarkdownUrl(Name, path) : MarkdownBuilder.MarkdownCodeQuote(Name);
            if (TypeArguments.Any()){
                name += "&lt;" + string.Join(",", TypeArguments.Select(t => t.Format(lib, mode))) + "&gt;";
            }

            return name;
        }
    }

    public class TypeScriptUnionType : TypeScriptType
    {
        public List<TypeScriptType> Types = new List<TypeScriptType>();

        public override string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return string.Join(" | ", Types.Select(t => t.Format(lib, mode)));
        }
    }

    public class TypeScriptReflectionType : TypeScriptType
    {
        public TypeScriptSignature Signature { get; set; }

        public override string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            if (Signature != null) {
                return $"({string.Join(", ", Signature.Parameters.Select(p => p.Format(lib, mode)))}) => {Signature.Type.Format(lib, mode)}";
            }

            return MarkdownBuilder.MarkdownCodeQuote("any");
        }

    }
    #endregion

    public class TypeScriptInterface : ITypeScriptContract, ITypeScriptImplemented
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string BeautifulName => Name + " interface";

        public ITypeScriptModule Module { get; private set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
        public List<TypeScriptType> ImplementedTypes { get; set; } = new List<TypeScriptType>();

        public TypeScriptInterface(ITypeScriptModule module)
        {
            Module = module;
        }

        public string GetPath()
        {
            return $"{Module.GetPath()}/Interfaces/{BeautifulName}";
        }
    }

    public class TypeScriptProperty: ITypeScriptFormatter
    {
        public string Name { get; set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsPublic { get; set; }
        public bool IsProtected { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsOptional { get; set; }
        public bool IsStatic { get; set; }
        public TypeScriptType Type { get; set; }
        public string DefaultValue { get; set; }

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return $"● {Name}{(IsOptional ? "?" : "")}: {Type.Format(lib)}{((DefaultValue != null) ? " = " + MarkdownBuilder.MarkdownCodeQuote(DefaultValue) : "")}";
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

    public class TypeScriptClass: ITypeScriptContract, ITypeScriptExtended, ITypeScriptImplemented
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ITypeScriptModule Module { get; private set; }
        public string BeautifulName => Name + " class";
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public TypeScriptMethod Constructor { get; set; }
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
        public List<TypeScriptType> ImplementedTypes { get; set; } = new List<TypeScriptType>();
        public List<TypeScriptType> ExtendedTypes { get; set; } = new List<TypeScriptType>();

        public TypeScriptClass(ITypeScriptModule module)
        {
            Module = module;
        }

        public string GetPath()
        {
            return $"{Module.GetPath()}/Classes/{BeautifulName}";
        }

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

            if (IsOptional || DefaultValue != null) {
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

        public ITypeScriptModule Module { get; private set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return "▸ " + Signature.Format(lib);
        }

        public TypeScriptFunction(ITypeScriptModule module)
        {
            Module = module;
        }

        public string GetPath()
        {
            return $"{Module.GetPath()}/Functions";
        }

    }

    public class TypeScriptEnumeration
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BeautifulName => Name + " enum";

        public ITypeScriptModule Module { get; set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public List<TypeScriptEnumerationMember> Members = new List<TypeScriptEnumerationMember>();

        public TypeScriptEnumeration(ITypeScriptModule module)
        {
            Module = module;
        }

        public string GetPath()
        {
            return $"{Module.GetPath()}/Enumerations/{BeautifulName}";
        }
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

        public ITypeScriptModule Module { get; private set; }
        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }
        public bool IsConst { get; set; }
        public bool IsLet { get; set; }
        public TypeScriptType Type { get; set; }
        public string DefaultValue { get; set; }

        public TypeScriptVariable(ITypeScriptModule module)
        {
            Module = module;
        }

        public string Format(ITypeScriptLibrary lib, FormatMode mode = FormatMode.Markdown)
        {
            return $"● {Name}:{Type.Format(lib)}{((DefaultValue != null) ? " = " + MarkdownBuilder.MarkdownCodeQuote(DefaultValue) : "")}";
        }

        public string GetPath()
        {
            return $"{Module.GetPath()}/Variables";
        }
    }

    public class TypeScriptNamespace: ITypeScriptModule
    {
        public string Name { get; set; }
        public string BeautifulName => Name + " namespace";

        public ITypeScriptModule Module { get; set; }

        public TypeScriptComment Comment { get; set; }
        public bool IsExported { get; set; }

        public List<TypeScriptClass> Classes { get; } = new List<TypeScriptClass>();
        public List<TypeScriptInterface> Interfaces { get; } = new List<TypeScriptInterface>();
        public List<TypeScriptFunction> Functions { get; } = new List<TypeScriptFunction>();
        public List<TypeScriptEnumeration> Enumerations { get; } = new List<TypeScriptEnumeration>();
        public List<TypeScriptNamespace> Namespaces { get; } = new List<TypeScriptNamespace>();
        public List<TypeScriptVariable> Variables { get; } = new List<TypeScriptVariable>();

        public TypeScriptNamespace(ITypeScriptModule module)
        {
            Module = module;
        }

        public string GetPath()
        {
            return $"{Module.GetPath()}/{BeautifulName}";
        }

        public string FindPathToType(string name)
        {
            var classType = Classes.Where(c => c.IsExported).FirstOrDefault(cl => cl.Name == name);
            if (classType != null) {
                return classType.GetPath();
            }

            var interfaceType = Interfaces.Where(i => i.IsExported).FirstOrDefault(cl => cl.Name == name);
            if (interfaceType != null) {
                return interfaceType.GetPath();
            }

            var enumType = Enumerations.Where(e => e.IsExported).FirstOrDefault(cl => cl.Name == name);
            if (enumType != null) {
                return enumType.GetPath();
            }

            foreach (var nspace in Namespaces.Where(n => n.IsExported)) {
                var path = nspace.FindPathToType(name);
                if (path != null) {
                    return path;
                }
            }

            return null;

        }

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

        public string GetPath()
        {
            return BeautifulName;
        }

        public string FindPathToType(string name)
        {
            var classType = Classes.Where(c => c.IsExported).FirstOrDefault(cl => cl.Name == name);
            if (classType != null) {
                return classType.GetPath();
            }

            var interfaceType = Interfaces.Where(i => i.IsExported).FirstOrDefault(cl => cl.Name == name);
            if (interfaceType != null) {
                return interfaceType.GetPath();
            }

            var enumType = Enumerations.Where(e => e.IsExported).FirstOrDefault(cl => cl.Name == name);
            if (enumType != null)  {
                return enumType.GetPath();
            }

            foreach (var nspace in Namespaces.Where(n => n.IsExported)) {
                var path = nspace.FindPathToType(name);
                if (path != null) {
                    return path;
                }
            }

            return null;
        }
    }

    public class TypeScriptComment
    {
        public string ShortText { get; set; }
        public string Text { get; set; }
        public string Returns { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }

    public class TypeScriptLibrary : ITypeScriptLibrary
    {
        public string RootPath {get; set;}

        public List<TypeScriptPackage> Packages = new List<TypeScriptPackage>();

        public string FindPathToType(string name)
        {
            foreach (var package in Packages) {
                var path = package.FindPathToType(name);
                if (path != null) {
                    return RootPath != null ? RootPath.CombineWithUri(path.MakeUriFromString()) : path.MakeUriFromString();
                }
            }

            return null;
        }
    }
}
