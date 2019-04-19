using System;

using aistdoc;

namespace typedoc
{
    class Program
    {
        static void Main(string[] args)
        {
            var generator = new TypeScriptDocGenerator(new string[] { "test.json" });
            generator.Generate(new FileSaver("dest", null));
        }
    }
}
