using System;
using System.Collections.Generic;
using System.Text;

namespace aistdoc
{
    public class ArticlePublishModel
    {
        public string SectionTitle { get; set; }

        public string SectionUri { get; set; }

        public string ArticleTitle { get; set; }

        public string ArticleUri { get; set; }

        public string ArticleBody { get; set; }

        public string ArticleExcerpt { get; set; }

        public bool IsSection { get; set; } = false;
    }
}
