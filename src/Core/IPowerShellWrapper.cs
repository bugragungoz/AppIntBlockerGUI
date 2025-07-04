using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;

namespace AppIntBlockerGUI.Core
{
    public interface IPowerShellWrapper : IDisposable
    {
        bool HadErrors { get; }
        Collection<ErrorRecord> Errors { get; }
        IPowerShellWrapper AddScript(string script);
        Task<Collection<PSObject>> InvokeAsync();
    }
} 