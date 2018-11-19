using System;
using System.Collections.Generic;
using System.Text;

namespace Aistant.KbService.Models
{
    public class AistantDocument {

        public string Id { get; set; }

        public string Title { get; set; }

        public int IndexNum { get; set; }

        public string Uri { get; set; }

        public string FullPath { get; set; }

        public DocItemKind Kind { get; set; }

        public List<AistantDocument> Items { get; set; }

        public string[] Tags { get; set; }
    }
}
