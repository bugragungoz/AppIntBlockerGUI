using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppIntBlockerGUI.Services
{
    public interface ISystemRestoreService
    {
        Task<bool> IsSystemRestoreEnabledAsync();
        Task<List<SystemRestoreService.RestorePoint>> GetRestorePointsAsync();
        Task<bool> CreateRestorePointAsync(string description);
        Task<bool> RestoreSystemAsync(uint sequenceNumber);
        Task<bool> DeleteRestorePointAsync(uint sequenceNumber);
        Task<int> GetRestorePointCountAsync();
        Task<int> GetAppIntBlockerRestorePointCountAsync();
        Task<string> GetSystemRestoreStatusAsync();
    }
} 