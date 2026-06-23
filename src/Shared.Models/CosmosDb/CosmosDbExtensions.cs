using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shared.Models.CosmosDb;

public static class CosmosDbExtensions
{
    public static IServiceCollection AddCosmosDb(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string connectionString = configuration["CosmosDbConnection"]!;
        string dbName = configuration["CosmosDbName"]!;

        if (environment.IsDevelopment())
        {
            CosmosClient devClient = new CosmosClientBuilder(connectionString)
                .WithHttpClientFactory(() => new HttpClient(
                    new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    }))
                .WithThrottlingRetryOptions(
                    TimeSpan.FromSeconds(10),
                    maxRetryAttemptsOnThrottledRequests: 5)
                .Build();

            InitializeDevContainersAsync(devClient, dbName).GetAwaiter().GetResult();

            services.AddSingleton(devClient);
        }
        else
        {
            services.AddSingleton(_ =>
                new CosmosClientBuilder(connectionString)
                    .WithThrottlingRetryOptions(
                        TimeSpan.FromSeconds(10),
                        maxRetryAttemptsOnThrottledRequests: 5)
                    .Build());
        }

        services.AddSingleton(sp =>
        {
            CosmosClient client = sp.GetRequiredService<CosmosClient>();
            return new EnrichedRecordsContainer(client.GetContainer(dbName, "enriched-records"));
        });

        services.AddSingleton(sp =>
        {
            CosmosClient client = sp.GetRequiredService<CosmosClient>();
            return new AuditMetricsContainer(client.GetContainer(dbName, "audit-metrics"));
        });

        services.AddSingleton(sp =>
        {
            CosmosClient client = sp.GetRequiredService<CosmosClient>();
            return new IdempotencyContainer(client.GetContainer(dbName, "idempotency-records"));
        });

        return services;
    }

    private static async Task InitializeDevContainersAsync(CosmosClient client, string dbName)
    {
        DatabaseResponse dbResponse = await client.CreateDatabaseIfNotExistsAsync(dbName);
        await dbResponse.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("enriched-records", "/partyId"));
        await dbResponse.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("audit-metrics", "/processedDate"));
        await dbResponse.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("idempotency-records", "/messageId"));
    }
}
