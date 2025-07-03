using System;
using System.Windows.Media;

namespace AppIntBlockerGUI.Models
{
    public class FirewallRuleModel
    {
        public string RuleName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ProgramPath { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string LocalPort { get; set; } = string.Empty;
        public string RemotePort { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool IsEnabled { get; set; }
        public string Profile { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public DateTime CreationTime { get; set; } = DateTime.Now;
        public bool IsAppIntBlockerRule { get; set; } = false;

        public string EnabledText => Enabled ? "Enabled" : "Disabled";
        
        public string RuleType
        {
            get
            {
                if (IsAppIntBlockerRule) return "AppIntBlocker";
                if (!string.IsNullOrEmpty(DisplayName) && (DisplayName.Contains("Core Networking") || DisplayName.Contains("Windows"))) return "System";
                return "User";
            }
        }

        public string ActionDisplay => Action switch
        {
            "Allow" => "🟢 Allow",
            "Block" => "🔴 Block",
            _ => Action
        };

        public string DirectionDisplay => Direction switch
        {
            "Inbound" => "⬇️ In",
            "Outbound" => "⬆️ Out",
            _ => Direction
        };

        public Brush DirectionColor => Direction.ToLower() switch
        {
            "inbound" => Brushes.LightBlue,
            "outbound" => Brushes.LightCoral,
            _ => Brushes.White
        };

        public Brush StatusColor => Status.ToLower() switch
        {
            "enabled" => Brushes.LightGreen,
            "disabled" => Brushes.Orange,
            _ => Brushes.Gray
        };
    }
} 