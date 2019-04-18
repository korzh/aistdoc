using System;

using aistdoc;

namespace typedoc
{
    class Program
    {
        static void Main(string[] args)
        {
            var parser = new TypeDocJsonParser(new string[] { "test.json"});

            var packages = parser.Parse();
        }
    }
}
