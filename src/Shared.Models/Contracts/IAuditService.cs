using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IAuditService
{
    Task FlushAsync(AuditRecord record);
}
