using System;
using System.Collections.Generic;
using System.Text;

namespace aistdoc
{
    internal interface IDocGenerator
    {
        int Generate(IArticleSaver saver);
    }
}
