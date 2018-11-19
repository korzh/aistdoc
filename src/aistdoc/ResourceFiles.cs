using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace aistdoc {
    internal static class ResourceFiles {

        public static Stream GetResourceStream(string resourceFolder, string resourceFileName) {
            var asmbl = typeof(Program).GetTypeInfo().Assembly;

            string[] nameParts = asmbl.FullName.Split(',');

            string resourceName = nameParts[0] + "." + resourceFolder + "." + resourceFileName;

            var resources = new List<string>(asmbl.GetManifestResourceNames());
            if (resources.Contains(resourceName))
                return asmbl.GetManifestResourceStream(resourceName);
            else
                return null;
        }

        public static string GetResourceAsString(string resourceFolder, string resourceFileName) {
            string fileContent;
            using (StreamReader sr = new StreamReader(GetResourceStream(resourceFolder, resourceFileName))) {
                fileContent = sr.ReadToEnd();
            }
            return fileContent;
        }
    }
}
