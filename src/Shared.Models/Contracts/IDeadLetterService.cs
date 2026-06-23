using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IDeadLetterService
{
    Task SendAsync(KafkaMessageContext context, string originalPayload, string reason, int attempt);
}
