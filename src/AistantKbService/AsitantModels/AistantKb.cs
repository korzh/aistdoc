using System;
using System.Collections.Generic;
using System.Text;

namespace Aistant.KbService.Models
{
    public class AistantKb {
        public string Id { get; set; }

        public string Moniker { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Lang { get; set; }

        public string SupportEmail { get; set; }

        public string Stylesheet { get; set; }

        public string Header { get; set; }

        public string Footer { get; set; }

        public string SyncUrl { get; set; }

        public string SyncMode { get; set; }

        public string TitleTemplate { get; set; }

        public string IconUrl { get; set; }

        public string ActiveThemeId { get; set; }

        public bool IsHidden { get; set; }
    }
}
