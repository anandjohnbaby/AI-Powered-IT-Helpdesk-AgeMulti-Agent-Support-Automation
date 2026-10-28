using System.ComponentModel;
using Helpdesk.Application.Common;
using Microsoft.SemanticKernel;

namespace Helpdesk.Application.Agent.Plugins;

public class ServerPlugin(IServerService servers)
{
    [KernelFunction("list_servers")]
    [Description("Lists all managed servers with their current state.")]
    public async Task<string> ListServersAsync(CancellationToken ct = default)
    {
        var list = await servers.ListAsync(ct);
        return list.Count == 0
            ? "No managed servers found."
            : string.Join("\n", list.Select(s => $"- {s.Name}: {s.State} ({s.Status})"));
    }

    [KernelFunction("check_server_status")]
    [Description("Checks the status of one managed server by name.")]
    public async Task<string> CheckServerStatusAsync(
        [Description("Exact server name, e.g. web-server")] string serverName, CancellationToken ct = default)
    {
        var s = await servers.GetAsync(serverName, ct);
        return s is null
            ? $"'{serverName}' is not a managed server."
            : $"{s.Name} ({s.Image}): state={s.State}, status={s.Status}";
    }

    [KernelFunction("restart_server")]
    [Description("Requests a restart of a managed server. HIGH RISK: needs IT approval.")]
    public string RestartServer(
        [Description("Exact server name")] string serverName,
        [Description("Why the restart is needed")] string reason = "")
        => throw new InvalidOperationException("High-risk action: must go through the approval workflow.");
}
