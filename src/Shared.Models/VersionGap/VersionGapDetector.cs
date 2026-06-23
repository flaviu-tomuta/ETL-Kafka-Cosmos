using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Shared.Models.VersionGap;

public sealed class VersionGapDetector : IVersionGapDetector
{
    private readonly IEntityApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VersionGapDetector> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly string _apiBaseUrl;

    public VersionGapDetector(
        IEntityApiClient apiClient,
        IMemoryCache cache,
        ILogger<VersionGapDetector> logger,
        TelemetryClient telemetryClient,
        IConfiguration configuration)
    {
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
        _telemetryClient = telemetryClient;
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? string.Empty;
    }

    public async Task<EntityData> ResolveDataAsync(
        int incomingVersion, int storedVersion, string entityId, EntityData messagePayloadData)
    {
        int gap = incomingVersion - storedVersion;

        if (gap < 2)
            return messagePayloadData;

        _logger.LogWarning(
            "VersionGapDetected {EntityId} stored={StoredVersion} incoming={IncomingVersion} gap={Gap}",
            entityId, storedVersion, incomingVersion, gap);

        string cacheKey = $"api-entity-{entityId}-v{incomingVersion}";
        if (_cache.TryGetValue(cacheKey, out EntityData? cached))
            return cached!;

        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        Stopwatch timer = Stopwatch.StartNew();
        bool success = false;

        try
        {
            EntityData data = await _apiClient.GetEntityAtVersionAsync(entityId, incomingVersion);
            success = true;
            _cache.Set(cacheKey, data, TimeSpan.FromSeconds(60));
            return data;
        }
        finally
        {
            timer.Stop();
            _telemetryClient.TrackDependency(new DependencyTelemetry
            {
                Name      = "EntityApi.GetEntityAtVersion",
                Target    = _apiBaseUrl,
                Data      = $"GET /entities/{entityId}/versions/{incomingVersion}",
                Duration  = timer.Elapsed,
                Success   = success,
                Timestamp = startTime
            });
        }
    }
}
