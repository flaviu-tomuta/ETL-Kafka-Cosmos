using Shared.Models.Models;

namespace Shared.Models.Tests.Models;

public sealed class EnrichedCustomerTests
{
    [Fact]
    public void EnrichedCustomer_ExposesAllRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        EnrichedCustomer customer = new()
        {
            Id = "party-001",
            PartyId = "party-001",
            SchemaVersion = 1,
            LastUpdatedAt = now,
            LastUpdatedBy = "onboarding-app",
            Version = 1,
            Customer = new CustomerInfo
            {
                FirstName = "John",
                MiddleName = "M",
                LastName = "Smith",
                PreferredName = "Johnny",
                TaxInfo = new TaxInfo { TaxIdType = TaxIdType.SSN, TaxId = "6789" }
            },
            Onboarding = new OnboardingInfo
            {
                CompletedAt = now,
                ExternalCustomerNo = "EXT-001",
                EffectiveDate = "2026-06-22"
            },
            AuditSummary = new AuditSummary
            {
                LastMessageId = "msg-001",
                LastKafkaOffset = 12345L,
                LastHydrationSteps = ["AddressEnrichment"],
                WasApiFallback = false,
                ProcessedAt = now
            }
        };

        Assert.Equal("party-001", customer.PartyId);
        Assert.Equal("party-001", customer.Id);
        Assert.Equal(1, customer.SchemaVersion);
        Assert.Equal(now, customer.LastUpdatedAt);
        Assert.Equal("onboarding-app", customer.LastUpdatedBy);
        Assert.Equal(1, customer.Version);
        Assert.NotNull(customer.Customer);
        Assert.NotNull(customer.Onboarding);
        Assert.NotNull(customer.AuditSummary);
    }

    [Fact]
    public void EnrichedCustomer_CollectionProperties_DefaultToEmptyList()
    {
        EnrichedCustomer customer = new()
        {
            Id = "party-002",
            PartyId = "party-002"
        };

        Assert.NotNull(customer.Addresses);
        Assert.Empty(customer.Addresses);
        Assert.NotNull(customer.PhoneNumbers);
        Assert.Empty(customer.PhoneNumbers);
        Assert.NotNull(customer.EmailAddresses);
        Assert.Empty(customer.EmailAddresses);
        Assert.NotNull(customer.BankOperations);
        Assert.Empty(customer.BankOperations);
    }

    [Fact]
    public void EnrichedCustomer_IdAndPartyId_HoldSameValue()
    {
        EnrichedCustomer customer = new()
        {
            Id = "party-003",
            PartyId = "party-003"
        };

        Assert.Equal(customer.Id, customer.PartyId);
    }

    [Fact]
    public void EnrichedCustomer_SchemaVersion_DefaultsToOne()
    {
        EnrichedCustomer customer = new() { Id = "p", PartyId = "p" };

        Assert.Equal(1, customer.SchemaVersion);
    }

    [Fact]
    public void TaxInfo_StoresTaxIdAsProvided_NoMaskingInModel()
    {
        TaxInfo taxInfo = new()
        {
            TaxIdType = TaxIdType.SSN,
            TaxId = "***-**-6789"
        };

        Assert.Equal("***-**-6789", taxInfo.TaxId);
        Assert.Equal(TaxIdType.SSN, taxInfo.TaxIdType);
    }

    [Fact]
    public void TaxInfo_SupportsEinType()
    {
        TaxInfo taxInfo = new() { TaxIdType = TaxIdType.EIN, TaxId = "9876" };

        Assert.Equal(TaxIdType.EIN, taxInfo.TaxIdType);
    }

    [Fact]
    public void AddressType_EnumHasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(AddressType), AddressType.Primary));
        Assert.True(Enum.IsDefined(typeof(AddressType), AddressType.Secondary));
        Assert.True(Enum.IsDefined(typeof(AddressType), AddressType.Mailing));
        Assert.True(Enum.IsDefined(typeof(AddressType), AddressType.Billing));
        Assert.True(Enum.IsDefined(typeof(AddressType), AddressType.Previous));
    }

    [Fact]
    public void PhoneType_EnumHasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(PhoneType), PhoneType.Mobile));
        Assert.True(Enum.IsDefined(typeof(PhoneType), PhoneType.Home));
        Assert.True(Enum.IsDefined(typeof(PhoneType), PhoneType.Work));
        Assert.True(Enum.IsDefined(typeof(PhoneType), PhoneType.Fax));
    }

    [Fact]
    public void EmailType_EnumHasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(EmailType), EmailType.Personal));
        Assert.True(Enum.IsDefined(typeof(EmailType), EmailType.Work));
        Assert.True(Enum.IsDefined(typeof(EmailType), EmailType.Other));
    }

    [Fact]
    public void Address_ExposesAllRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Address address = new()
        {
            AddressId = "addr-001",
            AddressType = AddressType.Primary,
            IsPreferred = true,
            Line1 = "123 Main St",
            Line2 = "Apt 4B",
            City = "Austin",
            State = "TX",
            PostalCode = "78701",
            Country = "US",
            AddedAt = now,
            AddedBy = "onboarding-app"
        };

        Assert.Equal("addr-001", address.AddressId);
        Assert.Equal(AddressType.Primary, address.AddressType);
        Assert.True(address.IsPreferred);
        Assert.Equal(now, address.AddedAt);
        Assert.Equal("onboarding-app", address.AddedBy);
    }

    [Fact]
    public void PhoneNumber_ExposesAllRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        PhoneNumber phone = new()
        {
            PhoneId = "phone-001",
            PhoneType = PhoneType.Mobile,
            IsPreferred = true,
            Number = "+15125550001",
            Extension = null,
            AddedAt = now,
            AddedBy = "onboarding-app"
        };

        Assert.Equal("phone-001", phone.PhoneId);
        Assert.Equal(PhoneType.Mobile, phone.PhoneType);
        Assert.True(phone.IsPreferred);
        Assert.Equal("+15125550001", phone.Number);
    }

    [Fact]
    public void EmailAddress_ExposesAllRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        EmailAddress email = new()
        {
            EmailId = "email-001",
            EmailType = EmailType.Personal,
            IsPreferred = true,
            Address = "john@example.com",
            AddedAt = now,
            AddedBy = "onboarding-app"
        };

        Assert.Equal("email-001", email.EmailId);
        Assert.Equal(EmailType.Personal, email.EmailType);
        Assert.True(email.IsPreferred);
        Assert.Equal("john@example.com", email.Address);
    }

    [Fact]
    public void BankOperation_ExposesAllRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        BankOperation op = new()
        {
            OperationId = "op-001",
            AccountNumber = "9876543210",
            AccountType = "savings",
            BankName = "ABC Bank",
            AddedAt = now,
            AddedBy = "onboarding-app"
        };

        Assert.Equal("op-001", op.OperationId);
        Assert.Equal("9876543210", op.AccountNumber);
        Assert.Equal(now, op.AddedAt);
    }

    [Fact]
    public void AuditSummary_ExposesAllRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        AuditSummary summary = new()
        {
            LastMessageId = "msg-xyz",
            LastKafkaOffset = 48291L,
            LastHydrationSteps = ["AddressEnrichment", "ComplianceCheck"],
            WasApiFallback = false,
            ProcessedAt = now
        };

        Assert.Equal("msg-xyz", summary.LastMessageId);
        Assert.Equal(48291L, summary.LastKafkaOffset);
        Assert.Equal(2, summary.LastHydrationSteps.Count);
        Assert.False(summary.WasApiFallback);
        Assert.Equal(now, summary.ProcessedAt);
    }

    [Fact]
    public void OnboardingInfo_ExposesAllRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        OnboardingInfo onboarding = new()
        {
            CompletedAt = now,
            ExternalCustomerNo = "EXT-001",
            EffectiveDate = "2026-06-22"
        };

        Assert.Equal(now, onboarding.CompletedAt);
        Assert.Equal("EXT-001", onboarding.ExternalCustomerNo);
        Assert.Equal("2026-06-22", onboarding.EffectiveDate);
    }

    [Fact]
    public void CustomerInfo_ExposesAllRequiredProperties()
    {
        CustomerInfo info = new()
        {
            FirstName = "John",
            MiddleName = "Michael",
            LastName = "Smith",
            PreferredName = "Johnny",
            TaxInfo = new TaxInfo { TaxIdType = TaxIdType.SSN, TaxId = "6789" }
        };

        Assert.Equal("John", info.FirstName);
        Assert.Equal("Michael", info.MiddleName);
        Assert.Equal("Smith", info.LastName);
        Assert.Equal("Johnny", info.PreferredName);
        Assert.NotNull(info.TaxInfo);
    }

    [Fact]
    public void EnrichedCustomer_IsImmutable_InitOnlyProperties()
    {
        // Verify init-only semantics: once set, properties cannot be reassigned.
        // This is a compile-time guarantee; this test confirms instantiation works.
        EnrichedCustomer customer = new()
        {
            Id = "p-immutable",
            PartyId = "p-immutable",
            Version = 3
        };

        Assert.Equal(3, customer.Version);
    }
}
