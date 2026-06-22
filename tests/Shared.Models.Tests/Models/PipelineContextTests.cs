using Shared.Models.Models;

namespace Shared.Models.Tests.Models;

public sealed class PipelineContextTests
{
    [Fact]
    public void KafkaMessageContext_AllProperties_AreAccessible()
    {
        DateTimeOffset receivedAt = DateTimeOffset.UtcNow;
        KafkaMessageContext context = new()
        {
            MessageId  = "msg-001",
            PartyId    = "party-001",
            Topic      = "onboarding",
            Partition  = 2,
            Offset     = 48291L,
            TopicRole  = "onboarding",
            ReceivedAt = receivedAt
        };

        Assert.Equal("msg-001",    context.MessageId);
        Assert.Equal("party-001",  context.PartyId);
        Assert.Equal("onboarding", context.Topic);
        Assert.Equal(2,            context.Partition);
        Assert.Equal(48291L,       context.Offset);
        Assert.Equal("onboarding", context.TopicRole);
        Assert.Equal(receivedAt,   context.ReceivedAt);
    }

    [Fact]
    public void HydrationContext_AllProperties_AreAccessible()
    {
        EntityData data = new();
        HydrationContext context = new()
        {
            EntityId        = "entity-001",
            MessageId       = "msg-001",
            TopicRole       = "onboarding",
            IncomingVersion = 5,
            StoredVersion   = 4,
            Data            = data,
            WasApiFallback  = true
        };

        Assert.Equal("entity-001", context.EntityId);
        Assert.Equal("msg-001",    context.MessageId);
        Assert.Equal("onboarding", context.TopicRole);
        Assert.Equal(5,            context.IncomingVersion);
        Assert.Equal(4,            context.StoredVersion);
        Assert.Same(data,          context.Data);
        Assert.True(context.WasApiFallback);
    }

    [Fact]
    public void HydrationContext_IsImmutable_WithExpressionCreatesNewInstance()
    {
        // init-only properties are compiler-enforced; this demonstrates runtime immutability:
        // a 'with' expression creates a NEW record, leaving the original unchanged.
        HydrationContext original = new()
        {
            EntityId        = "entity-001",
            MessageId       = "msg-001",
            TopicRole       = "onboarding",
            IncomingVersion = 3,
            StoredVersion   = 2,
            Data            = new EntityData(),
            WasApiFallback  = false
        };
        HydrationContext modified = original with { IncomingVersion = 4 };

        Assert.Equal(3, original.IncomingVersion);
        Assert.Equal(4, modified.IncomingVersion);
        Assert.NotSame(original, modified);
    }

    [Fact]
    public void EnrichmentResult_DefaultDuration_IsTimeSpanZero()
    {
        EnrichmentResult result = new() { StepName = "TestStep", Applied = false };

        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void EnrichmentResult_AllProperties_AreAccessible()
    {
        EntityData data = new();
        TimeSpan duration = TimeSpan.FromMilliseconds(150);
        EnrichmentResult result = new()
        {
            StepName = "AddressEnrichment",
            Applied  = true,
            Duration = duration,
            Data     = data
        };

        Assert.Equal("AddressEnrichment", result.StepName);
        Assert.True(result.Applied);
        Assert.Equal(duration,            result.Duration);
        Assert.Same(data,                 result.Data);
    }

    [Fact]
    public void EntityData_DefaultProperties_IsEmptyDictionary()
    {
        EntityData data = new();

        Assert.NotNull(data.Properties);
        Assert.Empty(data.Properties);
    }
}
