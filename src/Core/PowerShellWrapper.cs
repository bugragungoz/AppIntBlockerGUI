// <copyright file="PowerShellWrapper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Core
{
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using Microsoft.Extensions.ObjectPool;

    public class PowerShellWrapper : IPowerShellWrapper
    {
        private readonly ObjectPool<PowerShell> powerShellPool;
        private readonly PowerShell powerShellInstance;

        public PowerShellWrapper(ObjectPool<PowerShell> pool)
        {
            this.powerShellPool = pool;
            this.powerShellInstance = this.powerShellPool.Get();
        }

        public bool HadErrors => this.powerShellInstance.HadErrors;

        public Collection<ErrorRecord> Errors => this.powerShellInstance.Streams.Error.ReadAll() as Collection<ErrorRecord> ?? new Collection<ErrorRecord>();

        public IPowerShellWrapper AddScript(string script)
        {
            this.powerShellInstance.AddScript(script);
            return this;
        }

        public async Task<Collection<PSObject>> InvokeAsync()
        {
            var results = await this.powerShellInstance.InvokeAsync();
            if (results == null)
            {
                return new Collection<PSObject>();
            }

            return new Collection<PSObject>(results.ToList());
        }

        public void Dispose()
        {
            this.powerShellPool.Return(this.powerShellInstance);
        }
    }
} 