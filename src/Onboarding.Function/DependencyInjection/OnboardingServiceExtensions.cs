using Microsoft.Extensions.DependencyInjection;
using Onboarding.Function.Pipeline;
using Onboarding.Function.Pipeline.Steps;
using Shared.Models.Contracts;

namespace Onboarding.Function.DependencyInjection;

public static class OnboardingServiceExtensions
{
    public static IServiceCollection AddOnboardingServices(this IServiceCollection services)
    {
        services.AddScoped<IEnrichmentStep, AddressEnrichmentStep>();
        services.AddScoped<IEnrichmentStep, CreditCheckEnrichmentStep>();
        services.AddScoped<IEnrichmentStep, ComplianceEnrichmentStep>();
        services.AddScoped<IHydrationPipeline, HydrationPipeline>();
        return services;
    }
}
