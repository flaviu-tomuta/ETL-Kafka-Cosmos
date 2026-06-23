using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.CosmosDb;
using Shared.Models.Models;

namespace Shared.Models.Idempotency;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly Container _container;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(IdempotencyContainer container, ILogger<IdempotencyService> logger)
    {
        _container = container.Value;
        _logger = logger;
    }

    public async Task<bool> IsDuplicateAsync(string messageId)
    {
        try
        {
            await _container.ReadItemAsync<IdempotencyRecord>(
                messageId, new PartitionKey(messageId));
            _logger.LogInformation(
                "DuplicateMessageSkipped {MessageId}", messageId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(string messageId)
    {
        IdempotencyRecord record = new()
        {
            Id          = messageId,
            MessageId   = messageId,
            ProcessedAt = DateTimeOffset.UtcNow,
            Ttl         = 604800
        };

        try
        {
            await _container.CreateItemAsync(record, new PartitionKey(messageId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // 409 Conflict means a concurrent execution already marked this message processed — treat as success
        }
    }
}
