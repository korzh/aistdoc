using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace aistdoc
{

    #region TypeScriptTypes 

    public enum TypeScriptTokenKind
    {
        ExternalModule = 1,
        Module = 2,
        Function = 64,
        Class = 128,
        Interface = 256,
        Constructor = 512,
        Property = 1024,
        Method = 2048
    }

    public class TypeScriptType
    {
        public string Name { get; set; }
        public string Type { get; set; }

        public static TypeScriptType CreateTypeSctiptType(string type)
        {

            switch (type) {
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

        public virtual void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                Name = nameToken.ToString();
            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                Type = typeToken.ToString();
            }
        }
    }

    public class TypeScriptArrayType: TypeScriptType
    {
        public TypeScriptType ElementType { get; set; }

        public override void LoadFromJObject(JObject jobject)
        {
            base.LoadFromJObject(jobject);

            if (jobject.TryGetValue("elementType", out var elementTypeToken)) {
                var elementTypeJobj = elementTypeToken as JObject;

                if (elementTypeJobj.TryGetValue("type", out var typeToken)) {
                    ElementType = TypeScriptType.CreateTypeSctiptType(typeToken.ToString());
                    ElementType.LoadFromJObject(elementTypeJobj);
                }
            }
        }
    }

    public class TypeScriptReferenceType : TypeScriptType
    {
        public int Id { get; set; }
        public List<TypeScriptType> TypeArguments { get; set; } = new List<TypeScriptType>();

        public override void LoadFromJObject(JObject jobject)
        {
            base.LoadFromJObject(jobject);

            if (jobject.TryGetValue("id", out var idToken)) {
                Id = idToken.ToObject<int>();
            }

            if (jobject.TryGetValue("typeArguments", out var typeArgumentsToken)) {
                var typeArgumentObjs = typeArgumentsToken.ToObject<List<JObject>>();
                foreach (var typeArgObj in typeArgumentObjs) {
                    if (typeArgObj.TryGetValue("type", out var typeToken)) {
                        var typeArg = TypeScriptType.CreateTypeSctiptType(typeToken.ToString());
                        typeArg.LoadFromJObject(typeArgObj);
                        TypeArguments.Add(typeArg);
                    }
                }
            }
        }
    }

    public class TypeScriptUnionType : TypeScriptType
    {
        public List<TypeScriptType> Types = new List<TypeScriptType>();

        public override void LoadFromJObject(JObject jobject)
        {
            base.LoadFromJObject(jobject);

            if (jobject.TryGetValue("types", out var typesToken)) {

                var typeObjs = typesToken.ToObject<List<JObject>>();
                foreach (var typeObj in typeObjs) {
                    if (typeObj.TryGetValue("type", out var typeToken)) {
                        var type = TypeScriptType.CreateTypeSctiptType(typeToken.ToString());
                        type.LoadFromJObject(typeObj);
                        Types.Add(type);
                    }
                }
            }
        }
    }

    public class TypeScriptReflectionType : TypeScriptType
    {
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();

        public override void LoadFromJObject(JObject jobject) {
            base.LoadFromJObject(jobject);

            try
            {
                var signatureObj = jobject["declaration"]["signatures"].ToObject<List<JObject>>().First();
                Signature.LoadFromJObject(signatureObj);
            }
            catch {
                throw new TypeDocPatserException("Wrong reflection type declaration: " + jobject.Path);
            }
        }
    }
    #endregion

    public class TypeScriptInterface
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public bool IsExported { get; set; }
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
        public void LoadFromObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    IsExported = isExportedToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("children", out var childrenToken)) {
                var children = childrenToken.ToObject<List<JObject>>();
                foreach (var child in children) {
                    var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                    if (childKind == TypeScriptTokenKind.Property) {
                        var property = new TypeScriptProperty();
                        property.LoadFromJObject(child);
                        Properties.Add(property);
                    }
                    else if(childKind == TypeScriptTokenKind.Method) {
                        var method = new TypeScriptMethod();
                        method.LoadFromJObject(child);
                        Methods.Add(method);
                    }
                }
            }
        }
    }

    public class TypeScriptProperty {
        public string Name { get; set; }
        public string Comment { get; set; }
        public bool IsPublic { get; set; }
        public bool IsProteced { get; set; }
        public bool IsPrivate { get => !(IsProteced || IsPublic); }
        public bool IsOptional { get; set; }
        public TypeScriptType Type { get; set; }

        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isPublic", out var isPublicToken)) {
                    IsPublic = isPublicToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isProtected", out var isProtectedToken)) {
                    IsPublic = isProtectedToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isOptional", out var isOptionalToken)) {
                    IsPublic = isOptionalToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                var typeObj = typeToken.ToObject<JObject>();

                if (typeObj.TryGetValue("type", out var typeDefToken)) {
                    Type = TypeScriptType.CreateTypeSctiptType(typeDefToken.ToString());
                    Type.LoadFromJObject(typeObj);
                }
            }
        }
    }

    public class TypeScriptMethod
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public bool IsPublic { get; set; }
        public bool IsProtected { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsOptional { get; set; }
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();
        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isPublic", out var isPublicToken)) {
                    IsPublic = isPublicToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isProtected", out var isProtectedToken)) {
                    IsProtected = isProtectedToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isPrivate", out var isPrivateToken)) {
                    IsPrivate = isPrivateToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isOptional", out var isOptionalToken)) {
                    IsOptional = isOptionalToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("signatures", out var signatureToken)) {
                Signature.LoadFromJObject(signatureToken.ToObject<List<JObject>>().First());               
            }
        }
    }

    public class TypeScriptClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public bool IsExported { get; set; }

        public TypeScriptMethod Constructor { get; set; } = new TypeScriptMethod();
        public List<TypeScriptProperty> Properties { get; set; } = new List<TypeScriptProperty>();
        public List<TypeScriptMethod> Methods { get; set; } = new List<TypeScriptMethod>();
        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("id", out var idToken)) {
                Id = idToken.ToObject<int>();
            }

            if (jobject.TryGetValue("name", out var nameTokent)) {
                Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    IsExported = isExportedToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("children", out var childrenToken)) {
                var children = childrenToken.ToObject<List<JObject>>();

                foreach (var child in children) {
                    var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                    if (childKind == TypeScriptTokenKind.Property) {
                        var property = new TypeScriptProperty();
                        property.LoadFromJObject(child);
                        Properties.Add(property);
                    }
                    else if (childKind == TypeScriptTokenKind.Method) {
                        var method = new TypeScriptMethod();
                        method.LoadFromJObject(child);
                        Methods.Add(method);
                    }
                    else if (childKind == TypeScriptTokenKind.Constructor) {
                        Constructor.LoadFromJObject(child);
                    }
                }
            }
        }
    }

    public class TypeScriptParameter
    {
        public string Name { get; set; }
        public bool IsOptional { get; set; }
        public TypeScriptType Type { get; set; }
        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isOptional", out var isOptionalToken)){
                    IsOptional = isOptionalToken.ToObject<bool>();
                }
            }

            if (jobject.TryGetValue("type", out var typeToken)) {  
                var typeObj = typeToken.ToObject<JObject>();

                if (typeObj.TryGetValue("type", out var typeDefToken)) {
                    Type = TypeScriptType.CreateTypeSctiptType(typeDefToken.ToString());
                    Type.LoadFromJObject(typeObj);
                }
            }
        }
    }


    /// <summary>
    /// Describes a function signature with call name, parameters and retun type
    /// </summary>
    public class TypeScriptSignature
    {
        public string Name { get; set; }
        public List<TypeScriptParameter> Parameters { get; set; } = new List<TypeScriptParameter>();
        public TypeScriptType Type { get; set; }

        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                Name = nameToken.ToString();
            }

            if (jobject.TryGetValue("parameters", out var parametersToken)) {
                var parameterObjs = parametersToken.ToObject<List<JObject>>();

                foreach (var paramObj in parameterObjs) {
                    var parameter = new TypeScriptParameter();
                    parameter.LoadFromJObject(paramObj);
                    Parameters.Add(parameter);
                }
            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                var typeObj = typeToken.ToObject<JObject>();

                if (typeObj.TryGetValue("type", out var typeDefToken)) {
                    Type = TypeScriptType.CreateTypeSctiptType(typeDefToken.ToString());
                    Type.LoadFromJObject(typeObj);
                }
            }
        }
    }

    public class TypeScriptFunction
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public bool IsExported { get; set; }
        public TypeScriptSignature Signature { get; set; } = new TypeScriptSignature();
        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                Name = nameToken.ToString(); ;
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    IsExported = isExportedToken.ToObject<bool>();
                }
            }

            if (jobject.TryGetValue("signatures", out var signatureToken)) {
                Signature.LoadFromJObject(signatureToken.ToObject<List<JObject>>().First());
            }
        }
    }

    public class TypeScriptModule
    {
        public string Name { get; set; }
        public bool IsExported { get; set; }
        public List<TypeScriptClass> Classes = new List<TypeScriptClass>();
        public List<TypeScriptInterface> Interfaces = new List<TypeScriptInterface>();
        public List<TypeScriptFunction> Functions = new List<TypeScriptFunction>();

        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                Name = nameToken.ToString(); ;
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    IsExported = isExportedToken.ToObject<bool>();
                }
            }

            if (jobject.TryGetValue("children", out var childrenToken)) {
                var children = childrenToken.ToObject<List<JObject>>();

                foreach (var child in children) {
                    var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                    if (childKind == TypeScriptTokenKind.Class) {
                        var @class = new TypeScriptClass();
                        @class.LoadFromJObject(child);
                        Classes.Add(@class);
                    }
                    else if (childKind == TypeScriptTokenKind.Interface) {
                        var @interface = new TypeScriptInterface();
                        @interface.LoadFromObject(child);
                        Interfaces.Add(@interface);
                    }
                    else if (childKind == TypeScriptTokenKind.Function) {
                        var @function = new TypeScriptFunction();
                        function.LoadFromJObject(child);
                        Functions.Add(@function);
                    }
                }
            }


        }
    }

    public class TypeScriptPackage
    {
        public string Name { get; set; }

        public List<TypeScriptClass> Classes = new List<TypeScriptClass>();
        public List<TypeScriptInterface> Interfaces = new List<TypeScriptInterface>();
        public List<TypeScriptModule> Modules = new List<TypeScriptModule>();
        public List<TypeScriptFunction> Functions = new List<TypeScriptFunction>();

        public void LoadFromJObject(JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                Name = nameToken.ToString();
            }


            if (jobject.TryGetValue("children", out var externalModulesToken)) {

                //expects here extenral modules
                var externalModules = externalModulesToken.ToObject<List<JObject>>();

                foreach (var externalModule in externalModules) {
                    var kind = externalModule["kind"].ToObject<TypeScriptTokenKind>();

                    if (kind != TypeScriptTokenKind.ExternalModule) {
                        continue;
                    }

                    if (externalModule.TryGetValue("children",out var childrenToken)) {
                        var children = childrenToken.ToObject<List<JObject>>();

                        foreach (var child in children) {
                            var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                            if (childKind == TypeScriptTokenKind.Class)  {
                                var @class = new TypeScriptClass();
                                @class.LoadFromJObject(child);
                                Classes.Add(@class);
                            }
                            else if (childKind == TypeScriptTokenKind.Interface) {
                                var @interface = new TypeScriptInterface();
                                @interface.LoadFromObject(child);
                                Interfaces.Add(@interface);
                            }
                            else if (childKind == TypeScriptTokenKind.Function) {
                                var @function = new TypeScriptFunction();
                                function.LoadFromJObject(child);
                                Functions.Add(@function);
                            }
                            else if (childKind == TypeScriptTokenKind.Module) {
                                var module = new TypeScriptModule();
                                module.LoadFromJObject(child);
                                Modules.Add(module);
                            }
                        }
                    }


                }
            }

        }
    }
}
