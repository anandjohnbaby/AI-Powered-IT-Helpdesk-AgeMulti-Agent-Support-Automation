using Docker.DotNet;
using Docker.DotNet.Models;
using Helpdesk.Application.Common;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Services;

public class DockerOptions
{
    public string? Uri { get; set; }
    public string ManagedLabel { get; set; } = "helpdesk.managed=true";
}

/// Only containers labelled "helpdesk.managed=true" are visible/restartable.
public class DockerServerService : IServerService, IDisposable
{
    private readonly DockerClient _client;
    private readonly string _label;

    public DockerServerService(IOptions<DockerOptions> options)
    {
        var uri = options.Value.Uri ?? (OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock");
        _client = new DockerClientConfiguration(new Uri(uri)).CreateClient();
        _label = options.Value.ManagedLabel;
    }

    public async Task<IReadOnlyList<ServerInfo>> ListAsync(CancellationToken ct = default)
    {
        var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [_label] = true }
            }
        }, ct);

        return containers
            .Select(c => new ServerInfo(c.Names.First().TrimStart('/'), c.Image, c.State, c.Status))
            .OrderBy(s => s.Name)
            .ToList();
    }

    public async Task<ServerInfo?> GetAsync(string name, CancellationToken ct = default)
        => (await ListAsync(ct)).FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public async Task RestartAsync(string name, CancellationToken ct = default)
    {
        var server = await GetAsync(name, ct) ?? throw new InvalidOperationException($"'{name}' is not a managed server.");
        await _client.Containers.RestartContainerAsync(server.Name,
            new ContainerRestartParameters { WaitBeforeKillSeconds = 5 }, ct);
    }

    public void Dispose() => _client.Dispose();
}
