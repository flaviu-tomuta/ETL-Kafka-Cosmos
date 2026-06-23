using Amendment.Function.Handlers;
using Shared.Models.Contracts;
using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Amendment.Function.Tests.Handlers;

public sealed class ValidatorTests
{
    // ── AddOperationHandler.Validate — DuplicateRecord ─────────────────────

    [Fact]
    public void AddOperationHandler_Validate_DuplicateAddress_ReturnsRejected()
    {
        IOperationHandler handler = new AddOperationHandler();
        Address existing = new() { AddressId = "addr-001" };
        EnrichedCustomer entity = new() { Addresses = [existing] };
        Operation operation = new()
        {
            OperationType    = "add",
            Parameters       = new(),
            OperationDetails = new Dictionary<string, object> { ["addressId"] = "addr-001" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("DuplicateRecord", result.Reason);
    }

    [Fact]
    public void AddOperationHandler_Validate_DuplicatePhone_ReturnsRejected()
    {
        IOperationHandler handler = new AddOperationHandler();
        PhoneNumber existing = new() { PhoneId = "phone-001" };
        EnrichedCustomer entity = new() { PhoneNumbers = [existing] };
        Operation operation = new()
        {
            OperationType    = "add",
            Parameters       = new(),
            OperationDetails = new Dictionary<string, object> { ["phoneId"] = "phone-001" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("DuplicateRecord", result.Reason);
    }

    [Fact]
    public void AddOperationHandler_Validate_DuplicateEmail_ReturnsRejected()
    {
        IOperationHandler handler = new AddOperationHandler();
        EmailAddress existing = new() { EmailId = "email-001" };
        EnrichedCustomer entity = new() { EmailAddresses = [existing] };
        Operation operation = new()
        {
            OperationType    = "add",
            Parameters       = new(),
            OperationDetails = new Dictionary<string, object> { ["emailId"] = "email-001" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("DuplicateRecord", result.Reason);
    }

    [Fact]
    public void AddOperationHandler_Validate_DuplicateBankOperation_ReturnsRejected()
    {
        IOperationHandler handler = new AddOperationHandler();
        BankOperation existing = new() { OperationId = "op-001" };
        EnrichedCustomer entity = new() { BankOperations = [existing] };
        Operation operation = new()
        {
            OperationType    = "add",
            Parameters       = new(),
            OperationDetails = new Dictionary<string, object> { ["operationId"] = "op-001" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("DuplicateRecord", result.Reason);
    }

    // ── AddOperationHandler.Validate — IsPreferredConflict ─────────────────

    [Fact]
    public void AddOperationHandler_Validate_IsPreferredConflict_Address_ReturnsRejected()
    {
        IOperationHandler handler = new AddOperationHandler();
        Address preferred = new() { AddressId = "addr-001", IsPreferred = true };
        EnrichedCustomer entity = new() { Addresses = [preferred] };
        Operation operation = new()
        {
            OperationType    = "add",
            Parameters       = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["addressId"]   = "addr-new",
                ["isPreferred"] = true
            }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("IsPreferredConflict", result.Reason);
    }

    [Fact]
    public void AddOperationHandler_Validate_IsPreferredConflict_Phone_ReturnsRejected()
    {
        IOperationHandler handler = new AddOperationHandler();
        PhoneNumber preferred = new() { PhoneId = "phone-001", IsPreferred = true };
        EnrichedCustomer entity = new() { PhoneNumbers = [preferred] };
        Operation operation = new()
        {
            OperationType    = "add",
            Parameters       = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["phoneId"]     = "phone-new",
                ["isPreferred"] = true
            }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("IsPreferredConflict", result.Reason);
    }

    [Fact]
    public void AddOperationHandler_Validate_NewAddress_NoExistingPreferred_ReturnsValid()
    {
        IOperationHandler handler = new AddOperationHandler();
        EnrichedCustomer entity = new() { Addresses = [] };
        Operation operation = new()
        {
            OperationType    = "add",
            Parameters       = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["addressId"]   = "addr-new",
                ["isPreferred"] = true
            }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.False(result.IsNoOp);
    }

    // ── RemoveOperationHandler.Validate — RecordNotFound ───────────────────

    [Fact]
    public void RemoveOperationHandler_Validate_AddressNotFound_ReturnsRejected()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        EnrichedCustomer entity = new() { Addresses = [] };
        Operation operation = new()
        {
            OperationType = "remove",
            Parameters    = new Dictionary<string, object> { ["addressId"] = "addr-999" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("RecordNotFound", result.Reason);
    }

    [Fact]
    public void RemoveOperationHandler_Validate_PhoneNotFound_ReturnsRejected()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        EnrichedCustomer entity = new() { PhoneNumbers = [] };
        Operation operation = new()
        {
            OperationType = "remove",
            Parameters    = new Dictionary<string, object> { ["phoneId"] = "phone-999" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("RecordNotFound", result.Reason);
    }

    [Fact]
    public void RemoveOperationHandler_Validate_EmailNotFound_ReturnsRejected()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        EnrichedCustomer entity = new() { EmailAddresses = [] };
        Operation operation = new()
        {
            OperationType = "remove",
            Parameters    = new Dictionary<string, object> { ["emailId"] = "email-999" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("RecordNotFound", result.Reason);
    }

    [Fact]
    public void RemoveOperationHandler_Validate_BankOperationNotFound_ReturnsRejected()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        EnrichedCustomer entity = new() { BankOperations = [] };
        Operation operation = new()
        {
            OperationType = "remove",
            Parameters    = new Dictionary<string, object> { ["operationId"] = "op-999" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("RecordNotFound", result.Reason);
    }

    [Fact]
    public void RemoveOperationHandler_Validate_AddressExists_ReturnsValid()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        Address addr = new() { AddressId = "addr-001" };
        EnrichedCustomer entity = new() { Addresses = [addr] };
        Operation operation = new()
        {
            OperationType = "remove",
            Parameters    = new Dictionary<string, object> { ["addressId"] = "addr-001" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.False(result.IsNoOp);
    }

    // ── UpdateOperationHandler.Validate — RecordNotFound ───────────────────

    [Fact]
    public void UpdateOperationHandler_Validate_AddressNotFound_ReturnsRejected()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        EnrichedCustomer entity = new() { Addresses = [] };
        Operation operation = new()
        {
            OperationType    = "update",
            Parameters       = new Dictionary<string, object> { ["addressId"] = "addr-999" },
            OperationDetails = new Dictionary<string, object> { ["line1"] = "New St" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("RecordNotFound", result.Reason);
    }

    [Fact]
    public void UpdateOperationHandler_Validate_PhoneNotFound_ReturnsRejected()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        EnrichedCustomer entity = new() { PhoneNumbers = [] };
        Operation operation = new()
        {
            OperationType    = "update",
            Parameters       = new Dictionary<string, object> { ["phoneId"] = "phone-999" },
            OperationDetails = new Dictionary<string, object> { ["number"] = "+15125550001" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.True(result.IsRejected);
        Assert.Equal("RecordNotFound", result.Reason);
    }

    // ── UpdateOperationHandler.Validate — NoOp ────────────────────────────

    [Fact]
    public void UpdateOperationHandler_Validate_AddressDetailsIdentical_ReturnsNoOp()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        Address existing = new()
        {
            AddressId  = "addr-001",
            Line1      = "123 Main St",
            City       = "Austin",
            State      = "TX",
            PostalCode = "78701",
            Country    = "US"
        };
        EnrichedCustomer entity = new() { Addresses = [existing] };
        Operation operation = new()
        {
            OperationType    = "update",
            Parameters       = new Dictionary<string, object> { ["addressId"] = "addr-001" },
            OperationDetails = new Dictionary<string, object> { ["line1"] = "123 Main St" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.True(result.IsNoOp);
    }

    [Fact]
    public void UpdateOperationHandler_Validate_PhoneDetailsIdentical_ReturnsNoOp()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        PhoneNumber existing = new() { PhoneId = "phone-001", Number = "+15125550001" };
        EnrichedCustomer entity = new() { PhoneNumbers = [existing] };
        Operation operation = new()
        {
            OperationType    = "update",
            Parameters       = new Dictionary<string, object> { ["phoneId"] = "phone-001" },
            OperationDetails = new Dictionary<string, object> { ["number"] = "+15125550001" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.True(result.IsNoOp);
    }

    [Fact]
    public void UpdateOperationHandler_Validate_AddressDetailsDiffer_ReturnsValid()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        Address existing = new()
        {
            AddressId  = "addr-001",
            Line1      = "123 Main St",
            City       = "Austin",
            State      = "TX",
            PostalCode = "78701",
            Country    = "US"
        };
        EnrichedCustomer entity = new() { Addresses = [existing] };
        Operation operation = new()
        {
            OperationType    = "update",
            Parameters       = new Dictionary<string, object> { ["addressId"] = "addr-001" },
            OperationDetails = new Dictionary<string, object> { ["line1"] = "456 New Street" }
        };

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.False(result.IsNoOp);
    }

    // ── OperationHandlerResolver — UnknownOperationType ────────────────────

    [Fact]
    public void OperationHandlerResolver_Resolve_UnknownType_ThrowsBusinessRuleViolationException()
    {
        List<IOperationHandler> handlers =
        [
            new AddOperationHandler(),
            new RemoveOperationHandler(),
            new UpdateOperationHandler()
        ];

        BusinessRuleViolationException ex = Assert.Throws<BusinessRuleViolationException>(
            () => OperationHandlerResolver.Resolve(handlers, "delete", "entity-1", "msg-1"));

        Assert.Equal("UnknownOperationType", ex.RuleName);
        Assert.Equal("entity-1", ex.EntityId);
        Assert.Equal("msg-1", ex.MessageId);
    }

    [Fact]
    public void OperationHandlerResolver_Resolve_KnownType_ReturnsHandler()
    {
        List<IOperationHandler> handlers =
        [
            new AddOperationHandler(),
            new RemoveOperationHandler(),
            new UpdateOperationHandler()
        ];

        IOperationHandler handler = OperationHandlerResolver.Resolve(handlers, "add", "entity-1", "msg-1");

        Assert.Equal("add", handler.OperationType);
        Assert.IsType<AddOperationHandler>(handler);
    }
}
