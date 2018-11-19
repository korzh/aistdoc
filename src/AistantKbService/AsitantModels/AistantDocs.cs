using System;
using System.Collections.Generic;
using System.Text;

namespace Aistant.KbService.Models
{
    public class AistantDocs {

        public int Page { get; set; }

        public int Count { get; set; }

        public bool HasNextPage { get; set; }

        public bool HasPreviousPage { get; set; }

        public int Total { get; set; }

        public List<AistantDocument> Items {get; set;}
    }
}
