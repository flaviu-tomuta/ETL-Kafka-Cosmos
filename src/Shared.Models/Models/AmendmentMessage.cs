namespace Shared.Models.Models;

public sealed record AmendmentMessage
{
    public string       PartyId      { get; init; } = string.Empty;
    public string       EventType    { get; init; } = string.Empty;
    public AmendPayload AmendPayload { get; init; } = new();
}

public sealed record AmendPayload
{
    public string          ExternalCustomerNo { get; init; } = string.Empty;
    public string          EffectiveDate      { get; init; } = string.Empty;
    public List<Operation> OperationsPayload  { get; init; } = [];
}

public sealed record Operation
{
    public string                     OperationType    { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters       { get; init; } = new();
    public Dictionary<string, object> OperationDetails { get; init; } = new();
}
