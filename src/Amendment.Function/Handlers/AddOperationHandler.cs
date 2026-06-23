using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Amendment.Function.Handlers;

public sealed class AddOperationHandler : IOperationHandler
{
    public string OperationType => "add";

    public ValidationResult Validate(Operation operation, EnrichedCustomer storedEntity) =>
        new() { IsRejected = false, IsNoOp = false };

    public EnrichedCustomer Apply(Operation operation, EnrichedCustomer storedEntity)
    {
        Dictionary<string, object> details = operation.OperationDetails;

        if (details.ContainsKey("addressId"))
        {
            Address newItem = new()
            {
                AddressId   = GetString(details, "addressId"),
                AddressType = ParseEnum<AddressType>(details, "addressType", AddressType.Primary),
                IsPreferred = GetBool(details, "isPreferred"),
                Line1       = GetString(details, "line1"),
                Line2       = GetOptionalString(details, "line2"),
                City        = GetString(details, "city"),
                State       = GetString(details, "state"),
                PostalCode  = GetString(details, "postalCode"),
                Country     = GetString(details, "country"),
                AddedAt     = DateTimeOffset.UtcNow,
                AddedBy     = "amendment-app"
            };
            return storedEntity with { Addresses = [..storedEntity.Addresses, newItem] };
        }

        if (details.ContainsKey("phoneId"))
        {
            PhoneNumber newItem = new()
            {
                PhoneId     = GetString(details, "phoneId"),
                PhoneType   = ParseEnum<PhoneType>(details, "phoneType", PhoneType.Mobile),
                IsPreferred = GetBool(details, "isPreferred"),
                Number      = GetString(details, "number"),
                Extension   = GetOptionalString(details, "extension"),
                AddedAt     = DateTimeOffset.UtcNow,
                AddedBy     = "amendment-app"
            };
            return storedEntity with { PhoneNumbers = [..storedEntity.PhoneNumbers, newItem] };
        }

        if (details.ContainsKey("emailId"))
        {
            EmailAddress newItem = new()
            {
                EmailId     = GetString(details, "emailId"),
                EmailType   = ParseEnum<EmailType>(details, "emailType", EmailType.Personal),
                IsPreferred = GetBool(details, "isPreferred"),
                Address     = GetString(details, "address"),
                AddedAt     = DateTimeOffset.UtcNow,
                AddedBy     = "amendment-app"
            };
            return storedEntity with { EmailAddresses = [..storedEntity.EmailAddresses, newItem] };
        }

        if (details.ContainsKey("operationId"))
        {
            BankOperation newItem = new()
            {
                OperationId   = GetString(details, "operationId"),
                AccountNumber = GetString(details, "accountNumber"),
                AccountType   = GetString(details, "accountType"),
                BankName      = GetString(details, "bankName"),
                AddedAt       = DateTimeOffset.UtcNow,
                AddedBy       = "amendment-app"
            };
            return storedEntity with { BankOperations = [..storedEntity.BankOperations, newItem] };
        }

        return storedEntity;
    }

    private static string GetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out object? v) ? v?.ToString() ?? string.Empty : string.Empty;

    private static string? GetOptionalString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out object? v) ? v?.ToString() : null;

    private static bool GetBool(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out object? v) && v is bool b ? b : v is not null && bool.TryParse(v.ToString(), out bool parsed) && parsed;

    private static T ParseEnum<T>(Dictionary<string, object> dict, string key, T fallback) where T : struct, Enum =>
        dict.TryGetValue(key, out object? v) && Enum.TryParse<T>(v?.ToString(), out T result) ? result : fallback;
}
