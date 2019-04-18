using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace aistdoc
{

    internal class TypeDocPatserException : Exception {
        public TypeDocPatserException(string message) : base(message) {

        }
    }

    public class TypeDocJsonParser {

        private readonly List<string> _files = new List<string>();

        private List<TypeScriptPackage> _packages = null;

        public TypeDocJsonParser(IEnumerable<string> files)
        {
            _files.AddRange(files);
        }

        public List<TypeScriptPackage> Parse()
        {
            if (_packages == null) {
                _packages = new List<TypeScriptPackage>();

                JObject jobject;
                foreach (var file in _files) {
                    if (File.Exists(file)) {
                        jobject = JObject.Parse(File.ReadAllText(file));

                        var package = new TypeScriptPackage();
                        package.LoadFromJObject(jobject);

                        _packages.Add(package);
                    }
                }
            }

            return _packages;

        }


    }
}
