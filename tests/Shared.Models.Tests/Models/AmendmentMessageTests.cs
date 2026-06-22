using Shared.Models.Models;

namespace Shared.Models.Tests.Models;

public sealed class AmendmentMessageTests
{
    [Fact]
    public void AmendmentMessage_AllProperties_AreAccessible()
    {
        AmendPayload payload = new()
        {
            ExternalCustomerNo = "cust-001",
            EffectiveDate      = "2026-06-09",
            OperationsPayload  = []
        };
        AmendmentMessage message = new()
        {
            PartyId      = "12345",
            EventType    = "amend",
            AmendPayload = payload
        };

        Assert.Equal("12345", message.PartyId);
        Assert.Equal("amend", message.EventType);
        Assert.Same(payload,  message.AmendPayload);
    }

    [Fact]
    public void Operation_AllProperties_AreAccessible()
    {
        Dictionary<string, object> parameters       = new() { ["addressId"] = "addr-001" };
        Dictionary<string, object> operationDetails = new() { ["line1"]     = "456 New Street" };
        Operation op = new()
        {
            OperationType    = "update",
            Parameters       = parameters,
            OperationDetails = operationDetails
        };

        Assert.Equal("update",       op.OperationType);
        Assert.Same(parameters,      op.Parameters);
        Assert.Same(operationDetails, op.OperationDetails);
    }

    [Fact]
    public void Operation_DefaultParametersAndDetails_AreEmptyDictionaries()
    {
        Operation op = new() { OperationType = "add" };

        Assert.NotNull(op.Parameters);
        Assert.Empty(op.Parameters);
        Assert.NotNull(op.OperationDetails);
        Assert.Empty(op.OperationDetails);
    }

    [Fact]
    public void AmendPayload_DefaultOperationsPayload_IsEmptyList()
    {
        AmendPayload payload = new();

        Assert.NotNull(payload.OperationsPayload);
        Assert.Empty(payload.OperationsPayload);
    }
}
