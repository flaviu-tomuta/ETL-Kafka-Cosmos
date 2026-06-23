using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Models.Contracts;
using Shared.Models.DependencyInjection;
using Shared.Models.Models;
using Shared.Models.VersionGap;

namespace Shared.Models.Tests.VersionGap;

public sealed class VersionGapDetectorTests
{
    private sealed class CapturingChannel : ITelemetryChannel
    {
        public List<ITelemetry> Items { get; } = [];
        public bool? DeveloperMode { get; set; }
        public string EndpointAddress { get; set; } = string.Empty;
        public void Send(ITelemetry item) => Items.Add(item);
        public void Flush() { }
        public void Dispose() { }
    }

    private static (
        VersionGapDetector Sut,
        Mock<IEntityApiClient> MockApi,
        CapturingChannel Channel,
        Mock<ILogger<VersionGapDetector>> MockLogger
    ) Build()
    {
        Mock<IEntityApiClient> mockApi = new();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        Mock<ILogger<VersionGapDetector>> mockLogger = new();
        CapturingChannel channel = new();
        TelemetryConfiguration telemetryConfig = new() { TelemetryChannel = channel };
        TelemetryClient telemetryClient = new(telemetryConfig);
        Mock<IConfiguration> mockConfig = new();
        mockConfig.Setup(c => c["ApiBaseUrl"]).Returns("http://test-api");

        VersionGapDetector sut = new(
            mockApi.Object, cache, mockLogger.Object, telemetryClient, mockConfig.Object);

        return (sut, mockApi, channel, mockLogger);
    }

    // ─── AC1: gap < 2 → returns payload data, no API call ─────────────────────

    [Fact]
    public async Task ResolveDataAsync_WhenGapIsOne_ReturnsPayloadData_WithoutApiCall()
    {
        (VersionGapDetector sut, Mock<IEntityApiClient> mockApi, _, _) = Build();
        EntityData payloadData = new() { Properties = new() { ["key"] = "value" } };

        EntityData result = await sut.ResolveDataAsync(
            incomingVersion: 5, storedVersion: 4, entityId: "entity-1", messagePayloadData: payloadData);

        Assert.Same(payloadData, result);
        mockApi.Verify(a => a.GetEntityAtVersionAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResolveDataAsync_WhenGapIsZero_ReturnsPayloadData_WithoutApiCall()
    {
        (VersionGapDetector sut, Mock<IEntityApiClient> mockApi, _, _) = Build();
        EntityData payloadData = new();

        EntityData result = await sut.ResolveDataAsync(
            incomingVersion: 4, storedVersion: 4, entityId: "entity-1", messagePayloadData: payloadData);

        Assert.Same(payloadData, result);
        mockApi.Verify(a => a.GetEntityAtVersionAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    // ─── AC2: gap >= 2 → calls API and logs warning ────────────────────────────

    [Fact]
    public async Task ResolveDataAsync_WhenGapIsTwo_CallsApiClient()
    {
        (VersionGapDetector sut, Mock<IEntityApiClient> mockApi, _, _) = Build();
        mockApi.Setup(a => a.GetEntityAtVersionAsync("entity-1", 6)).ReturnsAsync(new EntityData());

        await sut.ResolveDataAsync(
            incomingVersion: 6, storedVersion: 4, entityId: "entity-1", messagePayloadData: new EntityData());

        mockApi.Verify(a => a.GetEntityAtVersionAsync("entity-1", 6), Times.Once);
    }

    [Fact]
    public async Task ResolveDataAsync_WhenGapIsTwo_LogsVersionGapDetectedWarning()
    {
        (VersionGapDetector sut, Mock<IEntityApiClient> mockApi, _, Mock<ILogger<VersionGapDetector>> mockLogger) = Build();
        mockApi.Setup(a => a.GetEntityAtVersionAsync(It.IsAny<string>(), It.IsAny<int>()))
               .ReturnsAsync(new EntityData());

        await sut.ResolveDataAsync(
            incomingVersion: 6, storedVersion: 4, entityId: "entity-1", messagePayloadData: new EntityData());

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("VersionGapDetected")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveDataAsync_WhenGapIsGreaterThanTwo_ReturnsApiData()
    {
        (VersionGapDetector sut, Mock<IEntityApiClient> mockApi, _, _) = Build();
        EntityData apiData = new();
        mockApi.Setup(a => a.GetEntityAtVersionAsync("entity-1", 10)).ReturnsAsync(apiData);

        EntityData result = await sut.ResolveDataAsync(
            incomingVersion: 10, storedVersion: 4, entityId: "entity-1", messagePayloadData: new EntityData());

        Assert.Same(apiData, result);
    }

    // ─── AC3: same entityId+version requested twice → cached on second call ────

    [Fact]
    public async Task ResolveDataAsync_WhenSameEntityVersionRequestedTwice_ReturnsCachedData_WithoutSecondApiCall()
    {
        (VersionGapDetector sut, Mock<IEntityApiClient> mockApi, _, _) = Build();
        EntityData apiData = new() { Properties = new() { ["cached"] = "yes" } };
        mockApi.Setup(a => a.GetEntityAtVersionAsync("entity-1", 6)).ReturnsAsync(apiData);

        await sut.ResolveDataAsync(6, 4, "entity-1", new EntityData());
        EntityData result = await sut.ResolveDataAsync(6, 4, "entity-1", new EntityData());

        mockApi.Verify(a => a.GetEntityAtVersionAsync("entity-1", 6), Times.Once);
        Assert.Same(apiData, result);
    }

    // ─── AC4: API call succeeds → TrackDependency called ──────────────────────

    [Fact]
    public async Task ResolveDataAsync_WhenApiCallSucceeds_TracksDependencyTelemetry()
    {
        (VersionGapDetector sut, Mock<IEntityApiClient> mockApi, CapturingChannel channel, _) = Build();
        mockApi.Setup(a => a.GetEntityAtVersionAsync(It.IsAny<string>(), It.IsAny<int>()))
               .ReturnsAsync(new EntityData());

        await sut.ResolveDataAsync(6, 4, "entity-1", new EntityData());

        DependencyTelemetry? dep = channel.Items.OfType<DependencyTelemetry>().FirstOrDefault();
        Assert.NotNull(dep);
        Assert.Equal("EntityApi.GetEntityAtVersion", dep!.Name);
        Assert.True(dep.Success);
    }

    // ─── DI registration ──────────────────────────────────────────────────────

    [Fact]
    public void AddSharedServices_RegistersIVersionGapDetector_AsScoped()
    {
        ServiceCollection services = new();
        services.AddSharedServices();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IVersionGapDetector));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
        Assert.Equal(typeof(VersionGapDetector), descriptor.ImplementationType);
    }

    [Fact]
    public void AddSharedServices_RegistersIEntityApiClient_AsHttpClient()
    {
        ServiceCollection services = new();
        services.AddSharedServices();

        bool registered = services.Any(d => d.ServiceType == typeof(IEntityApiClient));

        Assert.True(registered);
    }
}
