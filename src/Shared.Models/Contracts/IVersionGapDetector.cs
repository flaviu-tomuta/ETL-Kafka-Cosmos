using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IVersionGapDetector
{
    Task<EntityData> ResolveDataAsync(int incomingVersion, int storedVersion, string entityId, EntityData messagePayloadData);
}
