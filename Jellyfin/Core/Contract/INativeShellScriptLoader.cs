using System.Threading.Tasks;

namespace Jellyfin.Core.Contract;

/// <summary>
/// Defines the interface for loading the NativeShell javascript script.
/// </summary>
public interface INativeShellScriptLoader
{
    /// <summary>
    /// LoadNativeShellScript.
    /// </summary>
    /// <returns><see cref="Task"/>representing the asynchronous operation.</returns>
    Task<string> LoadNativeShellScript();
}
