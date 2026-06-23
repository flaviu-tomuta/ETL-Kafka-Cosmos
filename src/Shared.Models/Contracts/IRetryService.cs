using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IRetryService
{
    Task EnqueueAsync(KafkaMessageContext context, string originalPayload, int attemptCount);
}
