namespace Shared.Models.Exceptions;

public abstract class FunctionAppException : Exception
{
    public string EntityId { get; }
    public string MessageId { get; }

    protected FunctionAppException(
        string message,
        string entityId,
        string messageId,
        Exception? inner = null)
        : base(message, inner)
    {
        EntityId = entityId;
        MessageId = messageId;
    }
}
