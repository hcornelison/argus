using Argus.Contracts;
using Argus.Herald.Configuration;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace Argus.Herald.Ingest;

/// <summary>
/// Owns the gRPC channel to styx, performs host registration, and exposes the
/// generated client plus the API-key call metadata. Reused by all collectors.
/// </summary>
public class IngestConnection : IDisposable
{
    private readonly HeraldOptions _options;
    private readonly ILogger<IngestConnection> _logger;
    private readonly GrpcChannel _channel;
    private readonly SemaphoreSlim _registerLock = new(1, 1);

    public IngestService.IngestServiceClient Client { get; }
    public long HostId { get; private set; }
    public bool IsRegistered => HostId != 0;

    public IngestConnection(IOptions<HeraldOptions> options, ILogger<IngestConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = GrpcChannel.ForAddress(_options.StyxGrpcEndpoint);
        Client = new IngestService.IngestServiceClient(_channel);
    }

    /// <summary>Metadata carrying the per-agent API key on every call.</summary>
    public Metadata AuthHeaders() => new() { { "x-argus-api-key", _options.ApiKey } };

    /// <summary>Registers this host with styx (idempotent) and caches the host id.</summary>
    public async Task<bool> EnsureRegisteredAsync(CancellationToken ct)
    {
        if (IsRegistered) return true;
        await _registerLock.WaitAsync(ct);
        try
        {
            if (IsRegistered) return true;
            var resp = await Client.RegisterHostAsync(new RegisterHostRequest
            {
                MachineName = Environment.MachineName,
                OperatingSystem = RuntimeInformation.OSDescription,
                AgentVersion = typeof(IngestConnection).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            }, AuthHeaders(), cancellationToken: ct);
            HostId = resp.HostId;
            _logger.LogInformation("Registered with styx as host id {HostId}", HostId);
            return true;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("Host registration failed: {Status} {Detail}", ex.StatusCode, ex.Status.Detail);
            return false;
        }
        finally
        {
            _registerLock.Release();
        }
    }

    public void Dispose() => _channel.Dispose();
}
