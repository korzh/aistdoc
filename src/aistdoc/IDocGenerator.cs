using System;
using System.Collections.Generic;
using System.Text;

namespace aistdoc
{
    public interface IDocGenerator
    {

        /// <summary>
        /// Generate and saves documents to the saver.
        /// </summary>
        /// <param name="saver">The saver.</param>
        /// <returns>The number of articles</returns>
        int Generate(IArticleSaver saver);
    }
}
