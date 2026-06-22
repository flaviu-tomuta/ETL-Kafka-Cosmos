namespace Shared.Models.Exceptions;

public sealed class EnrichmentStepException : FunctionAppException
{
    public string StepName { get; }

    public EnrichmentStepException(
        string message,
        string entityId,
        string messageId,
        string stepName,
        Exception? inner = null)
        : base(message, entityId, messageId, inner)
    {
        StepName = stepName;
    }
}
