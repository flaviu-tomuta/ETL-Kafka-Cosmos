using Amendment.Function.Pipeline;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Amendment.Function.DependencyInjection;

public static class AmendmentServiceExtensions
{
    public static IServiceCollection AddAmendmentServices(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
            new ServiceBusClient(
                sp.GetRequiredService<IConfiguration>()["ServiceBusConnection"]
                ?? throw new InvalidOperationException("ServiceBusConnection is not configured")));

        services.AddScoped<IAmendmentPipeline, NullAmendmentPipeline>();

        // IRetryService and IDeadLetterService — real implementations wired in STORY-18
        services.AddScoped<IRetryService, NullRetryService>();
        services.AddScoped<IDeadLetterService, NullDeadLetterService>();

        // IAuditService — real implementation wired in STORY-21
        services.AddScoped<IAuditService, NullAuditService>();

        return services;
    }

    private sealed class NullRetryService : IRetryService
    {
        public Task EnqueueAsync(KafkaMessageContext context, string originalPayload, int attemptCount)
            => Task.CompletedTask;
    }

    private sealed class NullDeadLetterService : IDeadLetterService
    {
        public Task SendAsync(KafkaMessageContext context, string originalPayload, string reason, int attempt)
            => Task.CompletedTask;
    }

    private sealed class NullAuditService : IAuditService
    {
        public Task FlushAsync(AuditRecord record) => Task.CompletedTask;
    }
}
