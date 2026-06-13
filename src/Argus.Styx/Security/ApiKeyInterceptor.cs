using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;

namespace Argus.Styx.Security;

/// <summary>
/// Validates the "x-argus-api-key" metadata header on every ingest call against the
/// configured allowlist. The validated key hash is stored in ServerCallContext.UserState
/// under "ApiKeyHash" for the service to associate with a Host.
/// </summary>
public class ApiKeyInterceptor : Interceptor
{
    public const string HeaderName = "x-argus-api-key";
    public const string UserStateKey = "ApiKeyHash";

    private readonly IOptionsMonitor<IngestOptions> _options;
    private readonly ILogger<ApiKeyInterceptor> _logger;

    public ApiKeyInterceptor(IOptionsMonitor<IngestOptions> options, ILogger<ApiKeyInterceptor> logger)
    {
        _options = options;
        _logger = logger;
    }

    private void Authenticate(ServerCallContext context)
    {
        var raw = context.RequestHeaders.GetValue(HeaderName);
        if (string.IsNullOrWhiteSpace(raw))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing API key."));

        var hash = ApiKeyHasher.Hash(raw);
        if (!_options.CurrentValue.ApiKeyHashes.Contains(hash))
        {
            _logger.LogWarning("Rejected ingest call with unknown API key from {Peer}", context.Peer);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid API key."));
        }

        context.UserState[UserStateKey] = hash;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(requestStream, context);
    }
}
