using Microsoft.Extensions.ObjectPool;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;

namespace AppIntBlockerGUI.Core
{
    public class PowerShellWrapper : IPowerShellWrapper
    {
        private readonly ObjectPool<PowerShell> _powerShellPool;
        private readonly PowerShell _powerShellInstance;

        public PowerShellWrapper(ObjectPool<PowerShell> powerShellPool)
        {
            _powerShellPool = powerShellPool;
            _powerShellInstance = _powerShellPool.Get();
        }

        public bool HadErrors => _powerShellInstance.HadErrors;

        public Collection<ErrorRecord> Errors => _powerShellInstance.Streams.Error.ReadAll() as Collection<ErrorRecord> ?? new Collection<ErrorRecord>();

        public IPowerShellWrapper AddScript(string script)
        {
            _powerShellInstance.AddScript(script);
            return this;
        }

        public async Task<Collection<PSObject>> InvokeAsync()
        {
            return await _powerShellInstance.InvokeAsync();
        }

        public void Dispose()
        {
            _powerShellPool.Return(_powerShellInstance);
        }
    }
} 