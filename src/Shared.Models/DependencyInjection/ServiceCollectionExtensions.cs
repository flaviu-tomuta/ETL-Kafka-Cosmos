using Microsoft.Extensions.DependencyInjection;
using Shared.Models.ErrorClassification;

namespace Shared.Models.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IErrorClassifier, ErrorClassifier>();
        return services;
    }
}
