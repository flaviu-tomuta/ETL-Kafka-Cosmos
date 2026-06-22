namespace Shared.Models.Models;

public sealed record EnrichedCustomer
{
    public string Id                          { get; init; } = string.Empty;
    public string PartyId                     { get; init; } = string.Empty;
    public int    SchemaVersion               { get; init; } = 1;
    public DateTimeOffset LastUpdatedAt       { get; init; }
    public string LastUpdatedBy               { get; init; } = string.Empty;
    public int    Version                     { get; init; }

    public CustomerInfo?        Customer        { get; init; }
    public List<Address>        Addresses       { get; init; } = [];
    public List<PhoneNumber>    PhoneNumbers    { get; init; } = [];
    public List<EmailAddress>   EmailAddresses  { get; init; } = [];
    public List<BankOperation>  BankOperations  { get; init; } = [];
    public OnboardingInfo?      Onboarding      { get; init; }
    public AuditSummary?        AuditSummary    { get; init; }
}

public sealed record CustomerInfo
{
    public string FirstName     { get; init; } = string.Empty;
    public string? MiddleName   { get; init; }
    public string LastName      { get; init; } = string.Empty;
    public string? PreferredName { get; init; }
    public TaxInfo? TaxInfo     { get; init; }
}

public sealed record TaxInfo
{
    public TaxIdType TaxIdType { get; init; }
    public string TaxId        { get; init; } = string.Empty;
}

public sealed record Address
{
    public string      AddressId   { get; init; } = string.Empty;
    public AddressType AddressType { get; init; }
    public bool        IsPreferred { get; init; }
    public string      Line1       { get; init; } = string.Empty;
    public string?     Line2       { get; init; }
    public string      City        { get; init; } = string.Empty;
    public string      State       { get; init; } = string.Empty;
    public string      PostalCode  { get; init; } = string.Empty;
    public string      Country     { get; init; } = string.Empty;
    public DateTimeOffset AddedAt  { get; init; }
    public string      AddedBy     { get; init; } = string.Empty;
}

public sealed record PhoneNumber
{
    public string    PhoneId     { get; init; } = string.Empty;
    public PhoneType PhoneType   { get; init; }
    public bool      IsPreferred { get; init; }
    public string    Number      { get; init; } = string.Empty;
    public string?   Extension   { get; init; }
    public DateTimeOffset AddedAt { get; init; }
    public string    AddedBy     { get; init; } = string.Empty;
}

public sealed record EmailAddress
{
    public string    EmailId     { get; init; } = string.Empty;
    public EmailType EmailType   { get; init; }
    public bool      IsPreferred { get; init; }
    public string    Address     { get; init; } = string.Empty;
    public DateTimeOffset AddedAt { get; init; }
    public string    AddedBy     { get; init; } = string.Empty;
}

public sealed record BankOperation
{
    public string OperationId   { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string AccountType   { get; init; } = string.Empty;
    public string BankName      { get; init; } = string.Empty;
    public DateTimeOffset AddedAt { get; init; }
    public string AddedBy       { get; init; } = string.Empty;
}

public sealed record OnboardingInfo
{
    public DateTimeOffset CompletedAt        { get; init; }
    public string         ExternalCustomerNo { get; init; } = string.Empty;
    public string         EffectiveDate      { get; init; } = string.Empty;
}

public sealed record AuditSummary
{
    public string             LastMessageId       { get; init; } = string.Empty;
    public long               LastKafkaOffset     { get; init; }
    public List<string>       LastHydrationSteps  { get; init; } = [];
    public bool               WasApiFallback      { get; init; }
    public DateTimeOffset     ProcessedAt         { get; init; }
}
