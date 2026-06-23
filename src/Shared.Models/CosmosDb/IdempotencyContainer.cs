using Microsoft.Azure.Cosmos;

namespace Shared.Models.CosmosDb;

public sealed record IdempotencyContainer(Container Value);
