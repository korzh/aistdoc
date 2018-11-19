using System;
using System.Collections.Generic;
using System.Text;

namespace Aistant.KbService.Models
{
    public enum ArticleState {
        Draft,
        Published
    }

    public enum FormatType {
        Markdown,
        HTML,
        PlainText
    }

    public enum DocItemKind {
        Root    = 0,
        Article = 10,
        Section = 20
    }

    public class AistantArticle {

        public string Id { get; set; }

        public string TeamId { get; set; }

        public string KbId { get; set; }

        public string OwnerId { get; set; }

        public string IndexTitle { get; set; }

        public string Title { get; set; }

        public string ParentId {get; set;}

        public string[] Tags { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }

        public DateTime DatePublished { get; set; }

        public string Excerpt { get; set; }
        
        public string Content { get; set; }

        public int IntNo { get; set; }

        public int IndexNum { get; set; }

        public string Uri { get; set; }

        public string ExtraInfo { get; set; }

        public string ExtraProps { get; set; }

        public FormatType FormatType { get; set; }

        public ArticleState State { get; set; }

        public DocItemKind Kind { get; set; }

        public int LastVersion { get; set; }
        
        public int PubVersion { get; set; }
    }
}
