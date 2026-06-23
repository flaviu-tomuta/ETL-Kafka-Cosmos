using Amendment.Function.DependencyInjection;
using Amendment.Function.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Amendment.Function.Tests.Handlers;

public sealed class OperationHandlerTests
{
    // ── AddOperationHandler ────────────────────────────────────────────────

    [Fact]
    public void AddOperationHandler_OperationType_IsAdd()
    {
        IOperationHandler handler = new AddOperationHandler();
        Assert.Equal("add", handler.OperationType);
    }

    [Fact]
    public void AddOperationHandler_Validate_ReturnsNotRejectedNotNoOp()
    {
        IOperationHandler handler = new AddOperationHandler();
        Operation operation = new() { OperationType = "add", OperationDetails = new() { ["addressId"] = "addr-new" } };
        EnrichedCustomer entity = new();

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.False(result.IsNoOp);
    }

    [Fact]
    public void AddOperationHandler_Apply_AppendsAddressWithAuditFields()
    {
        IOperationHandler handler = new AddOperationHandler();
        EnrichedCustomer entity = new() { Addresses = [] };
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);

        Operation operation = new()
        {
            OperationType = "add",
            Parameters = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["addressId"]   = "addr-new",
                ["addressType"] = "Primary",
                ["line1"]       = "123 Main St",
                ["city"]        = "Austin",
                ["state"]       = "TX",
                ["postalCode"]  = "78701",
                ["country"]     = "US"
            }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Single(result.Addresses);
        Address added = result.Addresses[0];
        Assert.Equal("addr-new", added.AddressId);
        Assert.Equal("amendment-app", added.AddedBy);
        Assert.True(added.AddedAt >= before);
        Assert.Equal("123 Main St", added.Line1);
    }

    [Fact]
    public void AddOperationHandler_Apply_AppendsPhoneNumberWithAuditFields()
    {
        IOperationHandler handler = new AddOperationHandler();
        EnrichedCustomer entity = new() { PhoneNumbers = [] };

        Operation operation = new()
        {
            OperationType = "add",
            Parameters = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["phoneId"]   = "phone-new",
                ["phoneType"] = "Mobile",
                ["number"]    = "+15125550001"
            }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Single(result.PhoneNumbers);
        PhoneNumber added = result.PhoneNumbers[0];
        Assert.Equal("phone-new", added.PhoneId);
        Assert.Equal("amendment-app", added.AddedBy);
        Assert.Equal("+15125550001", added.Number);
    }

    [Fact]
    public void AddOperationHandler_Apply_AppendsEmailAddressWithAuditFields()
    {
        IOperationHandler handler = new AddOperationHandler();
        EnrichedCustomer entity = new() { EmailAddresses = [] };

        Operation operation = new()
        {
            OperationType = "add",
            Parameters = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["emailId"]   = "email-new",
                ["emailType"] = "Work",
                ["address"]   = "j@co.com"
            }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Single(result.EmailAddresses);
        EmailAddress added = result.EmailAddresses[0];
        Assert.Equal("email-new", added.EmailId);
        Assert.Equal("amendment-app", added.AddedBy);
        Assert.Equal("j@co.com", added.Address);
    }

    [Fact]
    public void AddOperationHandler_Apply_AppendsBankOperationWithAuditFields()
    {
        IOperationHandler handler = new AddOperationHandler();
        EnrichedCustomer entity = new() { BankOperations = [] };

        Operation operation = new()
        {
            OperationType = "add",
            Parameters = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["operationId"]   = "op-new",
                ["accountNumber"] = "9876543210",
                ["accountType"]   = "savings",
                ["bankName"]      = "ABC Bank"
            }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Single(result.BankOperations);
        BankOperation added = result.BankOperations[0];
        Assert.Equal("op-new", added.OperationId);
        Assert.Equal("amendment-app", added.AddedBy);
        Assert.Equal("ABC Bank", added.BankName);
    }

    [Fact]
    public void AddOperationHandler_Apply_PreservesExistingItemsInCollection()
    {
        IOperationHandler handler = new AddOperationHandler();
        Address existing = new() { AddressId = "addr-001", Line1 = "Old St", City = "X", State = "Y", PostalCode = "00000", Country = "US", AddedBy = "onboarding-app" };
        EnrichedCustomer entity = new() { Addresses = [existing] };

        Operation operation = new()
        {
            OperationType = "add",
            Parameters = new(),
            OperationDetails = new Dictionary<string, object>
            {
                ["addressId"] = "addr-002", ["addressType"] = "Secondary",
                ["line1"] = "New St", ["city"] = "B", ["state"] = "C",
                ["postalCode"] = "11111", ["country"] = "US"
            }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Equal(2, result.Addresses.Count);
        Assert.Contains(result.Addresses, a => a.AddressId == "addr-001");
        Assert.Contains(result.Addresses, a => a.AddressId == "addr-002");
    }

    // ── RemoveOperationHandler ─────────────────────────────────────────────

    [Fact]
    public void RemoveOperationHandler_OperationType_IsRemove()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        Assert.Equal("remove", handler.OperationType);
    }

    [Fact]
    public void RemoveOperationHandler_Validate_ReturnsNotRejectedNotNoOp()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        Operation operation = new() { OperationType = "remove", Parameters = new() { ["addressId"] = "addr-001" } };
        EnrichedCustomer entity = new();

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.False(result.IsNoOp);
    }

    [Fact]
    public void RemoveOperationHandler_Apply_RemovesMatchingAddress()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        Address addr = new() { AddressId = "addr-001" };
        EnrichedCustomer entity = new() { Addresses = [addr] };

        Operation operation = new() { OperationType = "remove", Parameters = new Dictionary<string, object> { ["addressId"] = "addr-001" } };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Empty(result.Addresses);
    }

    [Fact]
    public void RemoveOperationHandler_Apply_RemovesMatchingPhoneNumber()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        PhoneNumber phone = new() { PhoneId = "phone-001" };
        EnrichedCustomer entity = new() { PhoneNumbers = [phone] };

        Operation operation = new() { OperationType = "remove", Parameters = new Dictionary<string, object> { ["phoneId"] = "phone-001" } };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Empty(result.PhoneNumbers);
    }

    [Fact]
    public void RemoveOperationHandler_Apply_RemovesMatchingEmailAddress()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        EmailAddress email = new() { EmailId = "email-001" };
        EnrichedCustomer entity = new() { EmailAddresses = [email] };

        Operation operation = new() { OperationType = "remove", Parameters = new Dictionary<string, object> { ["emailId"] = "email-001" } };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Empty(result.EmailAddresses);
    }

    [Fact]
    public void RemoveOperationHandler_Apply_RemovesMatchingBankOperation()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        BankOperation bankOp = new() { OperationId = "op-001" };
        EnrichedCustomer entity = new() { BankOperations = [bankOp] };

        Operation operation = new() { OperationType = "remove", Parameters = new Dictionary<string, object> { ["operationId"] = "op-001" } };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Empty(result.BankOperations);
    }

    [Fact]
    public void RemoveOperationHandler_Apply_LeavesOtherAddressesUntouched()
    {
        IOperationHandler handler = new RemoveOperationHandler();
        EnrichedCustomer entity = new()
        {
            Addresses =
            [
                new Address { AddressId = "addr-001" },
                new Address { AddressId = "addr-002" }
            ]
        };

        Operation operation = new() { OperationType = "remove", Parameters = new Dictionary<string, object> { ["addressId"] = "addr-001" } };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Single(result.Addresses);
        Assert.Equal("addr-002", result.Addresses[0].AddressId);
    }

    // ── UpdateOperationHandler ─────────────────────────────────────────────

    [Fact]
    public void UpdateOperationHandler_OperationType_IsUpdate()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        Assert.Equal("update", handler.OperationType);
    }

    [Fact]
    public void UpdateOperationHandler_Validate_ReturnsNotRejectedNotNoOp()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        Operation operation = new() { OperationType = "update", Parameters = new() { ["addressId"] = "addr-001" } };
        EnrichedCustomer entity = new();

        ValidationResult result = handler.Validate(operation, entity);

        Assert.False(result.IsRejected);
        Assert.False(result.IsNoOp);
    }

    [Fact]
    public void UpdateOperationHandler_Apply_UpdatesSpecifiedAddressFieldsOnly()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        Address existing = new()
        {
            AddressId = "addr-001", Line1 = "Old St", Line2 = "Apt 1",
            City = "OldCity", State = "TX", PostalCode = "78701", Country = "US",
            AddedBy = "onboarding-app"
        };
        EnrichedCustomer entity = new() { Addresses = [existing] };

        Operation operation = new()
        {
            OperationType = "update",
            Parameters = new Dictionary<string, object> { ["addressId"] = "addr-001" },
            OperationDetails = new Dictionary<string, object> { ["line1"] = "456 New Street" }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Address updated = result.Addresses.Single(a => a.AddressId == "addr-001");
        Assert.Equal("456 New Street", updated.Line1);
        Assert.Equal("Apt 1", updated.Line2);
        Assert.Equal("OldCity", updated.City);
        Assert.Equal("onboarding-app", updated.AddedBy);
    }

    [Fact]
    public void UpdateOperationHandler_Apply_UpdatesSpecifiedPhoneFields()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        PhoneNumber existing = new() { PhoneId = "phone-001", Number = "+10000000000", PhoneType = PhoneType.Mobile, AddedBy = "onboarding-app" };
        EnrichedCustomer entity = new() { PhoneNumbers = [existing] };

        Operation operation = new()
        {
            OperationType = "update",
            Parameters = new Dictionary<string, object> { ["phoneId"] = "phone-001" },
            OperationDetails = new Dictionary<string, object> { ["number"] = "+15125550001" }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        PhoneNumber updated = result.PhoneNumbers.Single(p => p.PhoneId == "phone-001");
        Assert.Equal("+15125550001", updated.Number);
        Assert.Equal(PhoneType.Mobile, updated.PhoneType);
        Assert.Equal("onboarding-app", updated.AddedBy);
    }

    [Fact]
    public void UpdateOperationHandler_Apply_UpdatesSpecifiedEmailFields()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        EmailAddress existing = new() { EmailId = "email-001", Address = "old@co.com", EmailType = EmailType.Personal, AddedBy = "onboarding-app" };
        EnrichedCustomer entity = new() { EmailAddresses = [existing] };

        Operation operation = new()
        {
            OperationType = "update",
            Parameters = new Dictionary<string, object> { ["emailId"] = "email-001" },
            OperationDetails = new Dictionary<string, object> { ["address"] = "new@co.com" }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        EmailAddress updated = result.EmailAddresses.Single(e => e.EmailId == "email-001");
        Assert.Equal("new@co.com", updated.Address);
        Assert.Equal(EmailType.Personal, updated.EmailType);
    }

    [Fact]
    public void UpdateOperationHandler_Apply_UpdatesSpecifiedBankOperationFields()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        BankOperation existing = new() { OperationId = "op-001", BankName = "Old Bank", AccountNumber = "123", AccountType = "savings", AddedBy = "onboarding-app" };
        EnrichedCustomer entity = new() { BankOperations = [existing] };

        Operation operation = new()
        {
            OperationType = "update",
            Parameters = new Dictionary<string, object> { ["operationId"] = "op-001" },
            OperationDetails = new Dictionary<string, object> { ["bankName"] = "New Bank" }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        BankOperation updated = result.BankOperations.Single(b => b.OperationId == "op-001");
        Assert.Equal("New Bank", updated.BankName);
        Assert.Equal("123", updated.AccountNumber);
        Assert.Equal("onboarding-app", updated.AddedBy);
    }

    [Fact]
    public void UpdateOperationHandler_Apply_LeavesNonMatchingItemsUnchanged()
    {
        IOperationHandler handler = new UpdateOperationHandler();
        EnrichedCustomer entity = new()
        {
            Addresses =
            [
                new Address { AddressId = "addr-001", Line1 = "First St", City = "A", State = "B", PostalCode = "00000", Country = "US" },
                new Address { AddressId = "addr-002", Line1 = "Second St", City = "C", State = "D", PostalCode = "11111", Country = "US" }
            ]
        };

        Operation operation = new()
        {
            OperationType = "update",
            Parameters = new Dictionary<string, object> { ["addressId"] = "addr-001" },
            OperationDetails = new Dictionary<string, object> { ["line1"] = "Updated St" }
        };

        EnrichedCustomer result = handler.Apply(operation, entity);

        Assert.Equal("Updated St", result.Addresses.Single(a => a.AddressId == "addr-001").Line1);
        Assert.Equal("Second St", result.Addresses.Single(a => a.AddressId == "addr-002").Line1);
    }

    // ── AC4: Handler uniqueness per OperationType ──────────────────────────

    [Fact]
    public void AllHandlers_HaveUniqueOperationTypes()
    {
        List<IOperationHandler> handlers =
        [
            new AddOperationHandler(),
            new RemoveOperationHandler(),
            new UpdateOperationHandler()
        ];

        int distinctCount = handlers.Select(h => h.OperationType).Distinct().Count();
        Assert.Equal(handlers.Count, distinctCount);
    }

    // ── DI registration ────────────────────────────────────────────────────

    [Fact]
    public void AddAmendmentServices_RegistersAllThreeOperationHandlers()
    {
        ServiceCollection services = new();
        services.AddAmendmentServices();

        ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<IOperationHandler> handlers = provider.GetServices<IOperationHandler>();
        List<string> types = handlers.Select(h => h.OperationType).Order().ToList();

        Assert.Contains("add", types);
        Assert.Contains("remove", types);
        Assert.Contains("update", types);
    }
}
