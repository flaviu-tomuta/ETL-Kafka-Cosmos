using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Amendment.Function.Handlers;

public sealed class UpdateOperationHandler : IOperationHandler
{
    public string OperationType => "update";

    public ValidationResult Validate(Operation operation, EnrichedCustomer storedEntity) =>
        new() { IsRejected = false, IsNoOp = false };

    public EnrichedCustomer Apply(Operation operation, EnrichedCustomer storedEntity)
    {
        Dictionary<string, object> parameters = operation.Parameters;
        Dictionary<string, object> details    = operation.OperationDetails;

        if (parameters.TryGetValue("addressId", out object? addrId))
        {
            string id = addrId.ToString()!;
            List<Address> updated = storedEntity.Addresses
                .Select(a => a.AddressId == id ? ApplyAddressUpdate(a, details) : a)
                .ToList();
            return storedEntity with { Addresses = updated };
        }

        if (parameters.TryGetValue("phoneId", out object? phoneId))
        {
            string id = phoneId.ToString()!;
            List<PhoneNumber> updated = storedEntity.PhoneNumbers
                .Select(p => p.PhoneId == id ? ApplyPhoneUpdate(p, details) : p)
                .ToList();
            return storedEntity with { PhoneNumbers = updated };
        }

        if (parameters.TryGetValue("emailId", out object? emailId))
        {
            string id = emailId.ToString()!;
            List<EmailAddress> updated = storedEntity.EmailAddresses
                .Select(e => e.EmailId == id ? ApplyEmailUpdate(e, details) : e)
                .ToList();
            return storedEntity with { EmailAddresses = updated };
        }

        if (parameters.TryGetValue("operationId", out object? opId))
        {
            string id = opId.ToString()!;
            List<BankOperation> updated = storedEntity.BankOperations
                .Select(b => b.OperationId == id ? ApplyBankOperationUpdate(b, details) : b)
                .ToList();
            return storedEntity with { BankOperations = updated };
        }

        return storedEntity;
    }

    private static Address ApplyAddressUpdate(Address existing, Dictionary<string, object> details) =>
        existing with
        {
            Line1       = TryGet(details, "line1")       ?? existing.Line1,
            Line2       = TryGet(details, "line2")       ?? existing.Line2,
            City        = TryGet(details, "city")        ?? existing.City,
            State       = TryGet(details, "state")       ?? existing.State,
            PostalCode  = TryGet(details, "postalCode")  ?? existing.PostalCode,
            Country     = TryGet(details, "country")     ?? existing.Country,
            IsPreferred = TryGetBool(details, "isPreferred") ?? existing.IsPreferred,
            AddressType = TryGetEnum<AddressType>(details, "addressType") ?? existing.AddressType
        };

    private static PhoneNumber ApplyPhoneUpdate(PhoneNumber existing, Dictionary<string, object> details) =>
        existing with
        {
            Number      = TryGet(details, "number")    ?? existing.Number,
            Extension   = TryGet(details, "extension") ?? existing.Extension,
            IsPreferred = TryGetBool(details, "isPreferred") ?? existing.IsPreferred,
            PhoneType   = TryGetEnum<PhoneType>(details, "phoneType") ?? existing.PhoneType
        };

    private static EmailAddress ApplyEmailUpdate(EmailAddress existing, Dictionary<string, object> details) =>
        existing with
        {
            Address     = TryGet(details, "address")   ?? existing.Address,
            IsPreferred = TryGetBool(details, "isPreferred") ?? existing.IsPreferred,
            EmailType   = TryGetEnum<EmailType>(details, "emailType") ?? existing.EmailType
        };

    private static BankOperation ApplyBankOperationUpdate(BankOperation existing, Dictionary<string, object> details) =>
        existing with
        {
            AccountNumber = TryGet(details, "accountNumber") ?? existing.AccountNumber,
            AccountType   = TryGet(details, "accountType")   ?? existing.AccountType,
            BankName      = TryGet(details, "bankName")      ?? existing.BankName
        };

    private static string? TryGet(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out object? v) ? v?.ToString() : null;

    private static bool? TryGetBool(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out object? v)) return null;
        if (v is bool b) return b;
        return bool.TryParse(v?.ToString(), out bool parsed) ? parsed : null;
    }

    private static T? TryGetEnum<T>(Dictionary<string, object> dict, string key) where T : struct, Enum
    {
        if (!dict.TryGetValue(key, out object? v)) return null;
        return Enum.TryParse<T>(v?.ToString(), out T result) ? result : null;
    }
}
