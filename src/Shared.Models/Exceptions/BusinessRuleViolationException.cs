namespace Shared.Models.Exceptions;

public sealed class BusinessRuleViolationException : FunctionAppException
{
    public string RuleName { get; }

    public BusinessRuleViolationException(
        string message,
        string entityId,
        string messageId,
        string ruleName,
        Exception? inner = null)
        : base(message, entityId, messageId, inner)
    {
        RuleName = ruleName;
    }
}
