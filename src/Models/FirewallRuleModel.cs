// <copyright file="FirewallRuleModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Models
{
    using System;
    using System.Windows.Media;

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

        public string EnabledText => this.Enabled ? "Enabled" : "Disabled";

        public string RuleType
        {
            get
            {
                if (this.IsAppIntBlockerRule)
                {
                    return "AppIntBlocker";
                }

                if (!string.IsNullOrEmpty(this.DisplayName) && (this.DisplayName.Contains("Core Networking") || this.DisplayName.Contains("Windows")))
                {
                    return "System";
                }

                return "User";
            }
        }

        public string ActionDisplay => this.Action switch
        {
            "Allow" => "🟢 Allow",
            "Block" => "🔴 Block",
            _ => this.Action
        };

        public string DirectionDisplay => this.Direction switch
        {
            "Inbound" => "⬇️ In",
            "Outbound" => "⬆️ Out",
            _ => this.Direction
        };

        public Brush DirectionColor => this.Direction.ToLower() switch
        {
            "inbound" => Brushes.LightBlue,
            "outbound" => Brushes.LightCoral,
            _ => Brushes.White
        };

        public Brush StatusColor => this.Status.ToLower() switch
        {
            "enabled" => Brushes.LightGreen,
            "disabled" => Brushes.Orange,
            _ => Brushes.Gray
        };
    }
}
