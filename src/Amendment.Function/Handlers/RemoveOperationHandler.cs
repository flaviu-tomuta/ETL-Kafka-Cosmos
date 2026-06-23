using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Amendment.Function.Handlers;

public sealed class RemoveOperationHandler : IOperationHandler
{
    public string OperationType => "remove";

    public ValidationResult Validate(Operation operation, EnrichedCustomer storedEntity)
    {
        Dictionary<string, object> parameters = operation.Parameters;

        if (parameters.TryGetValue("addressId", out object? addrId))
        {
            string id = addrId.ToString()!;
            if (!storedEntity.Addresses.Any(a => a.AddressId == id))
                return new() { IsRejected = true, Reason = "RecordNotFound" };
        }
        else if (parameters.TryGetValue("phoneId", out object? phoneId))
        {
            string id = phoneId.ToString()!;
            if (!storedEntity.PhoneNumbers.Any(p => p.PhoneId == id))
                return new() { IsRejected = true, Reason = "RecordNotFound" };
        }
        else if (parameters.TryGetValue("emailId", out object? emailId))
        {
            string id = emailId.ToString()!;
            if (!storedEntity.EmailAddresses.Any(e => e.EmailId == id))
                return new() { IsRejected = true, Reason = "RecordNotFound" };
        }
        else if (parameters.TryGetValue("operationId", out object? opId))
        {
            string id = opId.ToString()!;
            if (!storedEntity.BankOperations.Any(b => b.OperationId == id))
                return new() { IsRejected = true, Reason = "RecordNotFound" };
        }

        return new() { IsRejected = false, IsNoOp = false };
    }

    public EnrichedCustomer Apply(Operation operation, EnrichedCustomer storedEntity)
    {
        Dictionary<string, object> parameters = operation.Parameters;

        if (parameters.TryGetValue("addressId", out object? addrId))
        {
            string id = addrId.ToString()!;
            return storedEntity with { Addresses = storedEntity.Addresses.Where(a => a.AddressId != id).ToList() };
        }

        if (parameters.TryGetValue("phoneId", out object? phoneId))
        {
            string id = phoneId.ToString()!;
            return storedEntity with { PhoneNumbers = storedEntity.PhoneNumbers.Where(p => p.PhoneId != id).ToList() };
        }

        if (parameters.TryGetValue("emailId", out object? emailId))
        {
            string id = emailId.ToString()!;
            return storedEntity with { EmailAddresses = storedEntity.EmailAddresses.Where(e => e.EmailId != id).ToList() };
        }

        if (parameters.TryGetValue("operationId", out object? opId))
        {
            string id = opId.ToString()!;
            return storedEntity with { BankOperations = storedEntity.BankOperations.Where(b => b.OperationId != id).ToList() };
        }

        return storedEntity;
    }
}
