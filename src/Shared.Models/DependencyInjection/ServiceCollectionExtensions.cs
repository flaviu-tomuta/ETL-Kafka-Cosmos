using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Shared.Models.Contracts;
using Shared.Models.ErrorClassification;
using Shared.Models.Idempotency;
using Shared.Models.VersionGap;

namespace Shared.Models.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IErrorClassifier, ErrorClassifier>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddMemoryCache();
        services.AddScoped<IVersionGapDetector, VersionGapDetector>();
        services.AddHttpClient<IEntityApiClient, EntityApiClient>()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromMilliseconds(200);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 10;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            });
        return services;
    }
}
