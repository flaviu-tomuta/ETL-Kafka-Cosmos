namespace Shared.Models.Contracts;

public interface IIdempotencyService
{
    Task<bool> IsDuplicateAsync(string messageId);
    Task MarkProcessedAsync(string messageId);
}
