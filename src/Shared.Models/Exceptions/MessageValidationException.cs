namespace Shared.Models.Exceptions;

public sealed class MessageValidationException : FunctionAppException
{
    public MessageValidationException(
        string message,
        string entityId,
        string messageId,
        Exception? inner = null)
        : base(message, entityId, messageId, inner)
    {
    }
}
