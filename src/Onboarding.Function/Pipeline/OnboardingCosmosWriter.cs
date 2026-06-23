using Microsoft.Azure.Cosmos;
using Shared.Models.CosmosDb;
using Shared.Models.Models;

namespace Onboarding.Function.Pipeline;

public sealed class OnboardingCosmosWriter : IOnboardingCosmosWriter
{
    private readonly Container _container;

    public OnboardingCosmosWriter(EnrichedRecordsContainer container)
    {
        _container = container.Value;
    }

    public async Task WriteAsync(EnrichedCustomer entity)
    {
        await _container.UpsertItemAsync(entity, new PartitionKey(entity.PartyId));
    }
}
