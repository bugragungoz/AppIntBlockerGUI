using AppIntBlockerGUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace AppIntBlockerGUI.Services
{
    public interface IFirewallService
    {
        Task<bool> BlockApplicationFiles(
            string path, 
            bool blockExe, 
            bool blockDll, 
            bool includeSubdirectories, 
            List<string> excludedKeywords, 
            List<string> excludedFiles, 
            ILoggingService logger, 
            CancellationToken cancellationToken);
        Task<bool> RemoveExistingRules(string applicationName, ILoggingService logger);
        Task<bool> RemoveSingleRule(string ruleName, ILoggingService logger);
        Task<List<string>> GetExistingRulesAsync(ILoggingService logger);
        Task<bool> CreateSystemRestorePoint(string description, ILoggingService logger);
        Task<List<FirewallRuleModel>> GetAllFirewallRulesAsync();
        Task<bool> DeleteRuleAsync(string ruleName);
        Task<bool> ToggleRuleAsync(string ruleName, bool enable);
        void OpenWindowsFirewallWithAdvancedSecurity();
    }
} 