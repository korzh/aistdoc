using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Mono.Cecil;

namespace aistdoc
{
    internal class PackageAssembly
    {
        public PackageAssembly(string targetFramework, AssemblyDefinition assembly)
        {
            TargetFramework = targetFramework;
            Assembly = assembly;
        }

        public string TargetFramework { get; }

        public AssemblyDefinition Assembly { get; }

        public XmlDocumentComment[] Comments { get; set; }
    }


    internal class NugetPackage
    {

        private NugetPackage(List<PackageAssembly> assemblies, ZipArchiveEntry nuspecEntry)
        {
            Assemblies = assemblies;
            if (nuspecEntry != null) {
                ReadNuspecFile(nuspecEntry);
            }
        }

        public string Id { get; private set;  }

        public string Description { get; private set; }

        public string Name => !string.IsNullOrEmpty(Id) ? Id : Assemblies.FirstOrDefault()?.Assembly?.Name.Name;

        public IEnumerable<string> TargetFrameworks => Assemblies.Select(a => a.TargetFramework)
            .Where(tfm => tfm != "")
            .Distinct();

        public List<PackageAssembly> Assemblies { get;  }

        public static NugetPackage Load(string packageFile, Regex pattern)
        {
            var assemblies = new List<PackageAssembly>();
            var assemblyDocs = new Dictionary<string, XmlDocumentComment[]>(StringComparer.InvariantCultureIgnoreCase);

            ZipArchiveEntry nuspec = null;
            using (var stream = File.OpenRead(packageFile))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var e in archive.Entries.Where(x => x.Name != "_._").OrderBy(x => x.FullName))
                {
                    var n = e.FullName;
                    var isLib = n.StartsWith("lib/", StringComparison.InvariantCultureIgnoreCase);
                    if (isLib && (n.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                                    n.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var parts = n.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        var tfm = (parts.Length >= 3) ? Uri.UnescapeDataString(parts[1].Trim().ToLowerInvariant()) : "";

                        if (n.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
                        {
                            try
                            {
                                var asmName = Path.GetFileNameWithoutExtension(n);
                                var comments = VSDocParser.ParseXmlComment(XDocument.Load(e.Open()));
                                if (!assemblyDocs.ContainsKey(asmName))
                                {
                                    assemblyDocs[asmName] = comments;
                                }
                                else {
                                    assemblyDocs[asmName] = assemblyDocs[asmName].Concat(comments).ToArray();
                                }
                            }
                            catch (Exception ex)
                            {

                            }

                        }
                        else
                        {
                            if (pattern == null || pattern.IsMatch(Path.GetFileNameWithoutExtension(e.Name))) {
                                using (var ms = new MemoryStream())
                                {
                                    e.Open().CopyTo(ms);
                                    ms.Position = 0;

                                    var ad = AssemblyDefinition.ReadAssembly(ms);
                                    assemblies.Add(new PackageAssembly(tfm, ad));
                                }
                            }
                        }
                    }
                    else if (n.EndsWith(".nuspec", StringComparison.InvariantCultureIgnoreCase)) {
                        nuspec = e;
                    }
                    else
                    {
                        // NOTHING TO DO;
                    }
                }

                foreach (var asm in assemblies) {
                    if (assemblyDocs.TryGetValue(asm.Assembly.Name.Name, out var comments))
                        asm.Comments = comments;
                }

                return new NugetPackage(assemblies, nuspec);

            }

        }

        private void ReadNuspecFile(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                var xdoc = XDocument.Load(stream);
                var ns = xdoc.Root.Name.Namespace;
                var meta = xdoc.Root.Elements().FirstOrDefault(x => x.Name.LocalName == "metadata");
                if (meta == null) {
                    throw new Exception("Failed to find metadata in " + xdoc);
                }
                Id = meta.Element(ns + "id").Value.Trim();
                Description = meta.Element(ns + "description").Value.Trim();
            }
        }
    }
}
