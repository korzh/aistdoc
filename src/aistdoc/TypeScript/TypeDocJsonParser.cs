using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace aistdoc
{

    /// <summary>
    ///  
    /// </summary>
    internal class TypeDocPatserException : Exception {
        public TypeDocPatserException(string message) : base(message) {

        }
    }


    /// <summary>
    /// 
    /// </summary>
    public class TypeDocJsonParser {

        private readonly List<string> _files = new List<string>();

        private TypeScriptLibrary _lib;

        public TypeDocJsonParser(IEnumerable<string> files)
        {
            _files.AddRange(files);
        }

        public TypeScriptLibrary Parse()
        {
            if (_lib == null) {
                _lib = new TypeScriptLibrary();

                JObject jobject;
                foreach (var file in _files) {
                    if (File.Exists(file)) {
                        jobject = JObject.Parse(File.ReadAllText(file));

                        var package = new TypeScriptPackage();
                        LoadFromJObject(package, jobject);

                        _lib.Packages.Add(package);
                    }
                }
            }

            return _lib;

        }

        private void LoadFromJObject(TypeScriptPackage package, JObject jobject) {

            if (jobject.TryGetValue("name", out var nameToken)) {
                package.Name = nameToken.ToString();
            }


            if (jobject.TryGetValue("children", out var externalModulesToken)) {

                //expects here extenral modules
                var externalModules = externalModulesToken.ToObject<List<JObject>>();

                foreach (var externalModule in externalModules) {
                    var kind = externalModule["kind"].ToObject<TypeScriptTokenKind>();

                    if (kind != TypeScriptTokenKind.ExternalModule) {
                        continue;
                    }

                    var isExported = false;
                    if (externalModule.TryGetValue("flags", out var flagsToken)) {
                        var flagsObj = flagsToken.ToObject<JObject>();

                        if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                            isExported = isExportedToken.ToObject<bool>();
                        }
                    }

                    if (externalModule.TryGetValue("children", out var childrenToken)) {
                        var children = childrenToken.ToObject<List<JObject>>();

                        foreach (var child in children) {
                            var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                            if (childKind == TypeScriptTokenKind.Class) {
                                var @class = new TypeScriptClass();
                                LoadFromJObject(@class, child);
                                if (!isExported)
                                {
                                    @class.IsExported = false;
                                }
                                package.Classes.Add(@class);
                            }
                            else if (childKind == TypeScriptTokenKind.Interface) {
                                var @interface = new TypeScriptInterface();
                                LoadFromJObject(@interface, child);
                                if (!isExported) {
                                    @interface.IsExported = false;
                                }
                                package.Interfaces.Add(@interface);
                            }
                            else if (childKind == TypeScriptTokenKind.Function) {
                                var function = new TypeScriptFunction();
                                LoadFromJObject(function, child);
                                if (!isExported) {
                                    function.IsExported = false;
                                }
                                package.Functions.Add(function);
                            }
                            else if (childKind == TypeScriptTokenKind.Namespace) {
                                var @namespace = new TypeScriptNamespace();
                                LoadFromJObject(@namespace, child);
                                if (!isExported) {
                                    @namespace.IsExported = false;
                                }
                                package.Namespaces.Add(@namespace);
                            }
                            else if (childKind == TypeScriptTokenKind.Enumeration) {
                                var @enum = new TypeScriptEnumeration();
                                LoadFromJObject(@enum, child);
                                if (!isExported) {
                                    @enum.IsExported = false;
                                }
                                package.Enumerations.Add(@enum);
                            }
                            else if (childKind == TypeScriptTokenKind.Varialbe) {
                                var @var = new TypeScriptVariable();
                                LoadFromJObject(var, child);
                                if (!isExported) {
                                    @var.IsExported = false;
                                }
                                package.Variables.Add(@var);
                            }
                        }
                    }


                    if (jobject.TryGetValue("comment", out var commentToken)) {
                        package.Comment = new TypeScriptComment();
                        LoadFromJObject(package.Comment, commentToken.ToObject<JObject>());
                    }
                }
            }
        }

        private void LoadFromJObject(TypeScriptNamespace @namespace, JObject jobject) {
            if (jobject.TryGetValue("name", out var nameToken)) {
                @namespace.Name = nameToken.ToString(); ;
            }

            if (jobject.TryGetValue("children", out var childrenToken)) {
                var children = childrenToken.ToObject<List<JObject>>();

                foreach (var child in children) {
                    var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                    if (childKind == TypeScriptTokenKind.Class) {
                        var @class = new TypeScriptClass();
                        LoadFromJObject(@class, child);
                        @namespace.Classes.Add(@class);
                    }
                    else if (childKind == TypeScriptTokenKind.Interface) {
                        var @interface = new TypeScriptInterface();
                        LoadFromJObject(@interface, child);
                        @namespace.Interfaces.Add(@interface);
                    }
                    else if (childKind == TypeScriptTokenKind.Function) {
                        var function = new TypeScriptFunction();
                        LoadFromJObject(function, child);
                        @namespace.Functions.Add(function);
                    }
                    else if (childKind == TypeScriptTokenKind.Namespace) {
                        var nspace= new TypeScriptNamespace();
                        LoadFromJObject(nspace, child);
                        @namespace.Namespaces.Add(nspace);
                    }
                    else if (childKind == TypeScriptTokenKind.Enumeration) {
                        var @enum = new TypeScriptEnumeration();
                        LoadFromJObject(@enum, child);
                        @namespace.Enumerations.Add(@enum);
                    }
                    else if (childKind == TypeScriptTokenKind.Varialbe) {
                        var @var = new TypeScriptVariable();
                        LoadFromJObject(var, child);
                        @namespace.Variables.Add(@var);
                    }
                }
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                @namespace.Comment = new TypeScriptComment();
                LoadFromJObject(@namespace.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptVariable variable, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                variable.Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken))
            {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    variable.IsExported = isExportedToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isConst", out var isConstToken)) {
                    variable.IsConst = isConstToken.ToObject<bool>();
                }
            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                var typeObj = typeToken.ToObject<JObject>();

                if (typeObj.TryGetValue("type", out var typeDefToken)) {
                    var type = TypeScriptType.CreateTypeSctiptType(typeDefToken.ToString());
                    LoadFromJObject(type, typeObj);
                    variable.Type = type;
                }
            }

            if (jobject.TryGetValue("defaultValue", out var defValToken)) {
                variable.DefaultValue = defValToken.ToString();
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                variable.Comment = new TypeScriptComment();
                LoadFromJObject(variable.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptFunction function, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                function.Name = nameToken.ToString(); ;
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)){
                    function.IsExported = isExportedToken.ToObject<bool>();
                }
            }

            if (jobject.TryGetValue("signatures", out var signatureToken)) {
                LoadFromJObject(function.Signature, signatureToken.ToObject<List<JObject>>().First());
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                function.Comment = new TypeScriptComment();
                LoadFromJObject(function.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptEnumeration @enum, JObject jobject)
        {

            if (jobject.TryGetValue("id", out var idToken)) {
                @enum.Id = idToken.ToObject<int>();
            }

            if (jobject.TryGetValue("name", out var nameToken)) {
                @enum.Name = nameToken.ToObject<string>();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    @enum.IsExported = isExportedToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("children", out var childrenToken)) {
                var children = childrenToken.ToObject<List<JObject>>();

                foreach (var child in children) {
                    var member = new TypeScriptEnumerationMember();
                    LoadFromJObject(member, child);
                    @enum.Members.Add(member);
                }
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                @enum.Comment = new TypeScriptComment();
                LoadFromJObject(@enum.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptEnumerationMember member, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                member.Name = nameToken.ToObject<string>();
            }

            if (jobject.TryGetValue("defaultValue", out var defValToken)) {
                member.DefaultValue = defValToken.ToObject<string>();
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                member.Comment = new TypeScriptComment();
                LoadFromJObject(member.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptInterface @interface, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                @interface.Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    @interface.IsExported = isExportedToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("children", out var childrenToken)) {
                var children = childrenToken.ToObject<List<JObject>>();

                foreach (var child in children) {
                    var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                    if (childKind == TypeScriptTokenKind.Property) {
                        var property = new TypeScriptProperty();
                        LoadFromJObject(property, child);
                        @interface.Properties.Add(property);
                    }
                    else if (childKind == TypeScriptTokenKind.Method) {
                        var method = new TypeScriptMethod();
                        LoadFromJObject(method, child);
                        @interface.Methods.Add(method);
                    }
                }
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                @interface.Comment = new TypeScriptComment();
                LoadFromJObject(@interface.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptClass @class, JObject jobject)
        {
            if (jobject.TryGetValue("id", out var idToken)){
                @class.Id = idToken.ToObject<int>();
            }

            if (jobject.TryGetValue("name", out var nameTokent)) {
                @class.Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isExported", out var isExportedToken)) {
                    @class.IsExported = isExportedToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("children", out var childrenToken)) {
                var children = childrenToken.ToObject<List<JObject>>();

                foreach (var child in children) {
                    var childKind = child["kind"].ToObject<TypeScriptTokenKind>();
                    if (childKind == TypeScriptTokenKind.Property) {
                        var property = new TypeScriptProperty();
                        LoadFromJObject(property, child);
                        @class.Properties.Add(property);
                    }
                    else if (childKind == TypeScriptTokenKind.Method) {
                        var method = new TypeScriptMethod();
                        LoadFromJObject(method, child);
                        @class.Methods.Add(method);
                    }
                    else if (childKind == TypeScriptTokenKind.Constructor) {
                        LoadFromJObject(@class.Constructor,child);
                    }
                }
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                @class.Comment = new TypeScriptComment();
                LoadFromJObject(@class.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptProperty property, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                property.Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isPublic", out var isPublicToken)) {
                    property.IsPublic = isPublicToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isProtected", out var isProtectedToken)) {
                    property.IsPublic = isProtectedToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isOptional", out var isOptionalToken)) {
                    property.IsPublic = isOptionalToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                var typeObj = typeToken.ToObject<JObject>();

                if (typeObj.TryGetValue("type", out var typeDefToken)) {
                    var type = TypeScriptType.CreateTypeSctiptType(typeDefToken.ToString());
                    LoadFromJObject(type, typeObj);
                    property.Type = type;
                }
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                property.Comment = new TypeScriptComment();
                LoadFromJObject(property.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptMethod method, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                method.Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isPublic", out var isPublicToken)) {
                    method.IsPublic = isPublicToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isProtected", out var isProtectedToken)) {
                    method.IsProtected = isProtectedToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isPrivate", out var isPrivateToken)) {
                    method.IsPrivate = isPrivateToken.ToObject<bool>();
                }

                if (flagsObj.TryGetValue("isOptional", out var isOptionalToken)) {
                    method.IsOptional = isOptionalToken.ToObject<bool>();
                }

            }

            if (jobject.TryGetValue("signatures", out var signatureToken)) {
                LoadFromJObject(method.Signature, signatureToken.ToObject<List<JObject>>().First());
            }

            if (jobject.TryGetValue("comment", out var commentToken)) {
                method.Comment = new TypeScriptComment();
                LoadFromJObject(method.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptSignature signature, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                signature.Name = nameToken.ToString();
            }

            if (jobject.TryGetValue("parameters", out var parametersToken)) {
                var parameterObjs = parametersToken.ToObject<List<JObject>>();

                foreach (var paramObj in parameterObjs) {
                    var parameter = new TypeScriptParameter();
                    LoadFromJObject(parameter, paramObj);
                    signature.Parameters.Add(parameter);
                }
            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                var typeObj = typeToken.ToObject<JObject>();

                if (typeObj.TryGetValue("type", out var typeDefToken)) {
                    var type = TypeScriptType.CreateTypeSctiptType(typeDefToken.ToString());
                    LoadFromJObject(type, typeObj);
                    signature.Type = type;
                }
            }
        }

        private void LoadFromJObject(TypeScriptParameter parameter, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameTokent)) {
                parameter.Name = nameTokent.ToString();
            }

            if (jobject.TryGetValue("flags", out var flagsToken)) {
                var flagsObj = flagsToken.ToObject<JObject>();

                if (flagsObj.TryGetValue("isOptional", out var isOptionalToken)) {
                    parameter.IsOptional = isOptionalToken.ToObject<bool>();
                }
            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                var typeObj = typeToken.ToObject<JObject>();

                if (typeObj.TryGetValue("type", out var typeDefToken)) {
                    var type = TypeScriptType.CreateTypeSctiptType(typeDefToken.ToString());
                    LoadFromJObject(type, typeObj);
                    parameter.Type = type;
                }
            }

            if (jobject.TryGetValue("defaultValue", out var defValToken)) {
                parameter.DefaultValue = defValToken.ToString();
            }


            if (jobject.TryGetValue("comment", out var commentToken)) {
                parameter.Comment = new TypeScriptComment();
                LoadFromJObject(parameter.Comment, commentToken.ToObject<JObject>());
            }
        }

        private void LoadFromJObject(TypeScriptReflectionType type, JObject jobject)
        {
            try {
                var signatureObj = jobject["declaration"]["signatures"].ToObject<List<JObject>>().First();
                LoadFromJObject(type.Signature, signatureObj);
            }
            catch {
                throw new TypeDocPatserException("Wrong reflection type declaration: " + jobject.Path);
            }
        }

        private void LoadFromJObject(TypeScriptArrayType type, JObject jobject)
        {

            if (jobject.TryGetValue("elementType", out var elementTypeToken)) {
                var elementTypeJobj = elementTypeToken as JObject;

                if (elementTypeJobj.TryGetValue("type", out var typeToken)) {
                    var elemType = TypeScriptType.CreateTypeSctiptType(typeToken.ToString());
                    LoadFromJObject(elemType, elementTypeJobj);
                    type.ElementType = elemType;
                }
            }
        }

        private void LoadFromJObject(TypeScriptReferenceType type, JObject jobject)
        {

            if (jobject.TryGetValue("id", out var idToken)) {
                type.Id = idToken.ToObject<int>();
            }

            if (jobject.TryGetValue("typeArguments", out var typeArgumentsToken)) {
                var typeArgumentObjs = typeArgumentsToken.ToObject<List<JObject>>();
                foreach (var typeArgObj in typeArgumentObjs) {
                    if (typeArgObj.TryGetValue("type", out var typeToken)) {
                        var typeArg = TypeScriptType.CreateTypeSctiptType(typeToken.ToString());
                        LoadFromJObject(typeArg, typeArgObj);
                        type.TypeArguments.Add(typeArg);
                    }
                }
            }
        }

        private void LoadFromJObject(TypeScriptUnionType type, JObject jobject)
        {

            if (jobject.TryGetValue("types", out var typesToken)) {

                var typeObjs = typesToken.ToObject<List<JObject>>();

                foreach (var typeObj in typeObjs) {
                    if (typeObj.TryGetValue("type", out var typeToken)) {
                        var typeArg = TypeScriptType.CreateTypeSctiptType(typeToken.ToString());
                        LoadFromJObject(typeArg, typeObj);
                        type.Types.Add(typeArg);
                    }
                }
            }
        }

        private void LoadFromJObject(TypeScriptType type, JObject jobject)
        {
            if (jobject.TryGetValue("name", out var nameToken)) {
                type.Name = nameToken.ToObject<string>();
            }

            if (jobject.TryGetValue("type", out var typeToken)) {
                type.Type = typeToken.ToObject<string>();
            }

            if (type is TypeScriptReflectionType) {
                LoadFromJObject((TypeScriptReflectionType)type, jobject);
            }
            else if (type is TypeScriptArrayType) {
                LoadFromJObject((TypeScriptArrayType)type, jobject);
            }
            else if (type is TypeScriptUnionType) {
                LoadFromJObject((TypeScriptUnionType)type, jobject);
            }
            else if (type is TypeScriptReferenceType) {
                LoadFromJObject((TypeScriptReferenceType)type, jobject);
            }
           
        }

        private void LoadFromJObject(TypeScriptComment comment, JObject jobject)
        {
            if (jobject.TryGetValue("shortText", out var shortTextToken)) {
                comment.ShortText = shortTextToken.ToString();
            }

            if (jobject.TryGetValue("text", out var textToken)) {
                comment.Text = textToken.ToString();
            }

            if (jobject.TryGetValue("returns", out var returnsToken)) {
                comment.Returns = returnsToken.ToString();
            }
        }
    }
}
