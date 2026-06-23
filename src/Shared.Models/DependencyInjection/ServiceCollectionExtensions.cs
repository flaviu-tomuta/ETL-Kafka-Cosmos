using Microsoft.Extensions.DependencyInjection;
using Shared.Models.Contracts;
using Shared.Models.ErrorClassification;
using Shared.Models.Idempotency;

namespace Shared.Models.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IErrorClassifier, ErrorClassifier>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        return services;
    }
}
