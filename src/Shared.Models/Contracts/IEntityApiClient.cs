using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IEntityApiClient
{
    Task<EntityData> GetEntityAtVersionAsync(string entityId, int version);
}
