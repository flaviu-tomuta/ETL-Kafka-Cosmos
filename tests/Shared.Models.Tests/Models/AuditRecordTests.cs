using Shared.Models.Models;

namespace Shared.Models.Tests.Models;

public sealed class AuditRecordTests
{
    private static AuditRecord BuildCompleteAuditRecord() => new()
    {
        Id            = "msg-abc-123",
        MessageId     = "msg-abc-123",
        PartyId       = "12345",
        TopicRole     = "amendment",
        ProcessedDate = "2026-06-09",
        ProcessedAt   = new DateTimeOffset(2026, 6, 9, 11, 0, 0, TimeSpan.Zero),
        KafkaContext  = new KafkaContextInfo
        {
            Topic      = "amendments",
            Partition  = 3,
            Offset     = 48291L,
            ReceivedAt = new DateTimeOffset(2026, 6, 9, 10, 59, 59, TimeSpan.Zero)
        },
        Versions = new VersionInfo
        {
            Stored         = 4,
            Incoming       = 5,
            Gap            = 1,
            WasApiFallback = false
        },
        Outcome       = "success",
        FailureReason = null,
        Hydration = new HydrationInfo
        {
            StepsApplied    = ["AddressEnrichment", "ComplianceCheck"],
            StepsSkipped    = ["CreditCheck"],
            TotalDurationMs = 142,
            StepBreakdown   =
            [
                new HydrationStepBreakdown { Step = "AddressEnrichment", DurationMs = 80, Applied = true  },
                new HydrationStepBreakdown { Step = "ComplianceCheck",   DurationMs = 62, Applied = true  },
                new HydrationStepBreakdown { Step = "CreditCheck",       DurationMs = 0,  Applied = false }
            ]
        },
        RetryInfo = new RetryInfo
        {
            AttemptNumber = 1,
            WasRetry      = false,
            DeadLettered  = false
        }
    };

    [Fact]
    public void ToCosmosDocument_ContainsKafkaContext()
    {
        AuditRecord record = BuildCompleteAuditRecord();
        AuditCosmosDocument doc = record.ToCosmosDocument();

        Assert.NotNull(doc.KafkaContext);
        Assert.Equal("amendments", doc.KafkaContext.Topic);
        Assert.Equal(3,            doc.KafkaContext.Partition);
        Assert.Equal(48291L,       doc.KafkaContext.Offset);
    }

    [Fact]
    public void ToCosmosDocument_ContainsVersions()
    {
        AuditRecord record = BuildCompleteAuditRecord();
        AuditCosmosDocument doc = record.ToCosmosDocument();

        Assert.NotNull(doc.Versions);
        Assert.Equal(4, doc.Versions.Stored);
        Assert.Equal(5, doc.Versions.Incoming);
        Assert.Equal(1, doc.Versions.Gap);
        Assert.False(doc.Versions.WasApiFallback);
    }

    [Fact]
    public void ToCosmosDocument_ContainsOutcome()
    {
        AuditRecord record = BuildCompleteAuditRecord();
        AuditCosmosDocument doc = record.ToCosmosDocument();

        Assert.Equal("success", doc.Outcome);
    }

    [Fact]
    public void ToCosmosDocument_ContainsHydration_WithAllSubSections()
    {
        AuditRecord record = BuildCompleteAuditRecord();
        AuditCosmosDocument doc = record.ToCosmosDocument();

        Assert.NotNull(doc.Hydration);
        Assert.Equal(2,   doc.Hydration.StepsApplied.Count);
        Assert.Single(doc.Hydration.StepsSkipped);
        Assert.Equal(142, doc.Hydration.TotalDurationMs);
        Assert.Equal(3,   doc.Hydration.StepBreakdown.Count);
    }

    [Fact]
    public void ToCosmosDocument_ContainsRetryInfo()
    {
        AuditRecord record = BuildCompleteAuditRecord();
        AuditCosmosDocument doc = record.ToCosmosDocument();

        Assert.NotNull(doc.RetryInfo);
        Assert.Equal(1,  doc.RetryInfo.AttemptNumber);
        Assert.False(doc.RetryInfo.WasRetry);
        Assert.False(doc.RetryInfo.DeadLettered);
    }

    [Fact]
    public void ToCosmosDocument_TtlIs15552000()
    {
        AuditRecord record = BuildCompleteAuditRecord();
        AuditCosmosDocument doc = record.ToCosmosDocument();

        Assert.Equal(15552000, doc.Ttl);
    }

    [Fact]
    public void AuditRecord_DefaultTtl_Is15552000()
    {
        AuditRecord record = new();

        Assert.Equal(15552000, record.Ttl);
    }

    [Fact]
    public void AuditRecord_ProcessedDate_AcceptsYYYYMMDDFormat()
    {
        AuditRecord record = new() { ProcessedDate = "2026-06-09" };

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", record.ProcessedDate);
    }
}
