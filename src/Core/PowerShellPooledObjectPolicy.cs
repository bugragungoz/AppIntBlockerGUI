// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PowerShellPooledObjectPolicy.cs" company="CROXX">
//   Copyright (c) CROXX. All rights reserved.
// </copyright>
// <summary>
//   Defines the object pooling policy for PowerShell instances.
//   This code was generated with the assistance of AI.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.ObjectPool;
using System.Management.Automation;

namespace AppIntBlockerGUI.Core
{
    /// <summary>
    /// Implements the <see cref="IPooledObjectPolicy{T}"/> for managing PowerShell instances in an object pool.
    /// This helps reuse PowerShell runspaces to improve performance.
    /// </summary>
    public class PowerShellPooledObjectPolicy : IPooledObjectPolicy<PowerShell>
    {
        /// <summary>
        /// Creates a new PowerShell instance.
        /// </summary>
        /// <returns>A new <see cref="PowerShell"/> instance.</returns>
        public PowerShell Create()
        {
            return PowerShell.Create();
        }

        /// <summary>
        /// Resets the state of a PowerShell instance before returning it to the pool.
        /// </summary>
        /// <param name="obj">The PowerShell instance to reset.</param>
        /// <returns>True if the object was successfully reset and can be returned to the pool; otherwise, false.</returns>
        public bool Return(PowerShell obj)
        {
            obj.Commands.Clear();
            return true;
        }
    }
} 