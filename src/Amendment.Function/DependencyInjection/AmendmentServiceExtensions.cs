using Amendment.Function.Handlers;
using Amendment.Function.Orchestrator;
using Amendment.Function.Pipeline;
using Amendment.Function.ServiceBus;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Models.Contracts;

namespace Amendment.Function.DependencyInjection;

public static class AmendmentServiceExtensions
{
    public static IServiceCollection AddAmendmentServices(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
            new ServiceBusClient(
                sp.GetRequiredService<IConfiguration>()["ServiceBusConnection"]
                ?? throw new InvalidOperationException("ServiceBusConnection is not configured")));

        services.AddScoped<IOperationHandler, AddOperationHandler>();
        services.AddScoped<IOperationHandler, RemoveOperationHandler>();
        services.AddScoped<IOperationHandler, UpdateOperationHandler>();

        services.AddScoped<IAmendmentOrchestrator, AmendmentOrchestrator>();
        services.AddScoped<IAmendmentPipeline, AmendmentPipeline>();

        services.AddScoped<IRetryService, RetryService>();
        services.AddScoped<IDeadLetterService, DeadLetterService>();


        return services;
    }

}
