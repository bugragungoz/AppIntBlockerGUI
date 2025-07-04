// <copyright file="ISystemRestoreService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

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
