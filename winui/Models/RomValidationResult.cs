using System;
using System.Collections.Generic;

namespace Box86_WinUI.Models
{
    public class RomValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsConfigured { get; set; }
        public string Message { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string BadgeStyleKey { get; set; } = "AttentionDotInfoBadgeStyle";
        public List<string> DetectedComponents { get; } = new();
    }
}
