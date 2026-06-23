using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IOperationHandler
{
    string OperationType { get; }
    ValidationResult Validate(Operation operation, EnrichedCustomer storedEntity);
    EnrichedCustomer Apply(Operation operation, EnrichedCustomer storedEntity);
}
