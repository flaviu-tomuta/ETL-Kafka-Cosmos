using Shared.Models.Contracts;
using Shared.Models.Exceptions;

namespace Amendment.Function.Handlers;

public static class OperationHandlerResolver
{
    public static IOperationHandler Resolve(
        IEnumerable<IOperationHandler> handlers,
        string operationType,
        string entityId,
        string messageId)
    {
        IOperationHandler? handler = handlers.FirstOrDefault(h => h.OperationType == operationType);
        if (handler is null)
            throw new BusinessRuleViolationException(
                $"Unknown operation type '{operationType}'",
                entityId,
                messageId,
                "UnknownOperationType");
        return handler;
    }
}
