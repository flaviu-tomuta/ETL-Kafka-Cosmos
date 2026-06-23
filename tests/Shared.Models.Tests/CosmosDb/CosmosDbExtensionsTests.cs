using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shared.Models.CosmosDb;

namespace Shared.Models.Tests.CosmosDb;

public sealed class CosmosDbExtensionsTests
{
    // Convert.ToBase64String(new byte[64]) produces a valid 88-char base64 key accepted by CosmosClientBuilder
    private static readonly string ValidTestKey = Convert.ToBase64String(new byte[64]);

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDbConnection"] = $"AccountEndpoint=https://localhost:8081/;AccountKey={ValidTestKey}",
                ["CosmosDbName"] = "test-db"
            })
            .Build();

    private static IHostEnvironment ProductionEnvironment() =>
        new FakeHostEnvironment { EnvironmentName = Environments.Production };

    [Fact]
    public void AddCosmosDb_NonDev_RegistersCosmosClientAsSingleton()
    {
        ServiceCollection services = new();
        services.AddCosmosDb(BuildConfig(), ProductionEnvironment());
        using ServiceProvider provider = services.BuildServiceProvider();

        CosmosClient client = provider.GetRequiredService<CosmosClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddCosmosDb_NonDev_CosmosClientIsSingleton_SameInstanceReturned()
    {
        ServiceCollection services = new();
        services.AddCosmosDb(BuildConfig(), ProductionEnvironment());
        using ServiceProvider provider = services.BuildServiceProvider();

        CosmosClient first = provider.GetRequiredService<CosmosClient>();
        CosmosClient second = provider.GetRequiredService<CosmosClient>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddCosmosDb_NonDev_RegistersEnrichedRecordsContainer()
    {
        ServiceCollection services = new();
        services.AddCosmosDb(BuildConfig(), ProductionEnvironment());
        using ServiceProvider provider = services.BuildServiceProvider();

        EnrichedRecordsContainer container = provider.GetRequiredService<EnrichedRecordsContainer>();

        Assert.NotNull(container);
        Assert.NotNull(container.Value);
    }

    [Fact]
    public void AddCosmosDb_NonDev_RegistersAuditMetricsContainer()
    {
        ServiceCollection services = new();
        services.AddCosmosDb(BuildConfig(), ProductionEnvironment());
        using ServiceProvider provider = services.BuildServiceProvider();

        AuditMetricsContainer container = provider.GetRequiredService<AuditMetricsContainer>();

        Assert.NotNull(container);
        Assert.NotNull(container.Value);
    }

    [Fact]
    public void AddCosmosDb_NonDev_RegistersIdempotencyContainer()
    {
        ServiceCollection services = new();
        services.AddCosmosDb(BuildConfig(), ProductionEnvironment());
        using ServiceProvider provider = services.BuildServiceProvider();

        IdempotencyContainer container = provider.GetRequiredService<IdempotencyContainer>();

        Assert.NotNull(container);
        Assert.NotNull(container.Value);
    }

    [Fact]
    public void AddCosmosDb_NonDev_ContainerWrappersAreSingletons()
    {
        ServiceCollection services = new();
        services.AddCosmosDb(BuildConfig(), ProductionEnvironment());
        using ServiceProvider provider = services.BuildServiceProvider();

        EnrichedRecordsContainer first = provider.GetRequiredService<EnrichedRecordsContainer>();
        EnrichedRecordsContainer second = provider.GetRequiredService<EnrichedRecordsContainer>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddCosmosDb_NonDev_ReturnsIServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection returned = services.AddCosmosDb(BuildConfig(), ProductionEnvironment());

        Assert.Same(services, returned);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
