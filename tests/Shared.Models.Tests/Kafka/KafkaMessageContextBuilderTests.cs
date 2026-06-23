using Shared.Models.Exceptions;
using Shared.Models.Kafka;
using Shared.Models.Models;

namespace Shared.Models.Tests.Kafka;

public class KafkaMessageContextBuilderTests
{
    private static readonly Dictionary<string, string> ValidHeaders = new()
    {
        ["messageId"] = "msg-001"
    };

    [Fact]
    public void Build_ValidHeaders_ReturnsContextWithMessageIdFromHeader()
    {
        KafkaMessageContext ctx = KafkaMessageContextBuilder.Build(
            headers: ValidHeaders,
            partyId: "party-001",
            topic: "onboarding",
            partition: 0,
            offset: 100L,
            topicRole: "onboarding");

        Assert.Equal("msg-001", ctx.MessageId);
    }

    [Fact]
    public void Build_ValidHeaders_ReturnsContextWithAllProperties()
    {
        KafkaMessageContext ctx = KafkaMessageContextBuilder.Build(
            headers: ValidHeaders,
            partyId: "party-002",
            topic: "onboarding-topic",
            partition: 3,
            offset: 42999L,
            topicRole: "onboarding");

        Assert.Equal("party-002", ctx.PartyId);
        Assert.Equal("onboarding-topic", ctx.Topic);
        Assert.Equal(3, ctx.Partition);
        Assert.Equal(42999L, ctx.Offset);
        Assert.Equal("onboarding", ctx.TopicRole);
    }

    [Fact]
    public void Build_ValidHeaders_ReceivedAtIsApproximatelyUtcNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        KafkaMessageContext ctx = KafkaMessageContextBuilder.Build(
            headers: ValidHeaders,
            partyId: "party-003",
            topic: "onboarding",
            partition: 0,
            offset: 1L,
            topicRole: "onboarding");

        DateTimeOffset after = DateTimeOffset.UtcNow;

        Assert.True(ctx.ReceivedAt >= before);
        Assert.True(ctx.ReceivedAt <= after);
    }

    [Fact]
    public void Build_MissingMessageIdHeader_ThrowsMessageValidationException()
    {
        Dictionary<string, string> headersWithoutMessageId = new()
        {
            ["someOtherHeader"] = "value"
        };

        MessageValidationException ex = Assert.Throws<MessageValidationException>(() =>
            KafkaMessageContextBuilder.Build(
                headers: headersWithoutMessageId,
                partyId: "party-004",
                topic: "onboarding",
                partition: 0,
                offset: 1L,
                topicRole: "onboarding"));

        Assert.Equal("unknown", ex.EntityId);
        Assert.Equal("unknown", ex.MessageId);
    }

    [Fact]
    public void Build_EmptyMessageIdHeader_ThrowsMessageValidationException()
    {
        Dictionary<string, string> headersWithEmptyMessageId = new()
        {
            ["messageId"] = string.Empty
        };

        Assert.Throws<MessageValidationException>(() =>
            KafkaMessageContextBuilder.Build(
                headers: headersWithEmptyMessageId,
                partyId: "party-005",
                topic: "onboarding",
                partition: 0,
                offset: 1L,
                topicRole: "onboarding"));
    }

    [Fact]
    public void Build_MissingMessageIdHeader_ExceptionMessageDescribesHeaderAbsence()
    {
        Dictionary<string, string> emptyHeaders = [];

        MessageValidationException ex = Assert.Throws<MessageValidationException>(() =>
            KafkaMessageContextBuilder.Build(
                headers: emptyHeaders,
                partyId: "party-006",
                topic: "onboarding",
                partition: 0,
                offset: 1L,
                topicRole: "onboarding"));

        Assert.Contains("messageId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildLogScope_ReturnsAllSixKafkaContextProperties()
    {
        KafkaMessageContext ctx = new()
        {
            MessageId  = "msg-scope-01",
            PartyId    = "party-scope-01",
            Topic      = "onboarding",
            Partition  = 2,
            Offset     = 500L,
            TopicRole  = "onboarding",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        Dictionary<string, object> scope = KafkaMessageContextBuilder.BuildLogScope(ctx);

        Assert.Equal("msg-scope-01", scope["MessageId"]);
        Assert.Equal("party-scope-01", scope["PartyId"]);
        Assert.Equal("onboarding", scope["Topic"]);
        Assert.Equal(2, scope["Partition"]);
        Assert.Equal(500L, scope["Offset"]);
        Assert.Equal("onboarding", scope["TopicRole"]);
    }

    [Fact]
    public void BuildLogScope_ScopeContainsExactlySixEntries()
    {
        KafkaMessageContext ctx = new()
        {
            MessageId  = "msg-scope-02",
            PartyId    = "party-scope-02",
            Topic      = "amendment",
            Partition  = 1,
            Offset     = 1000L,
            TopicRole  = "amendment",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        Dictionary<string, object> scope = KafkaMessageContextBuilder.BuildLogScope(ctx);

        Assert.Equal(6, scope.Count);
    }

    [Fact]
    public void Build_AmendmentTopicRole_ReturnsContextWithAmendmentTopicRole()
    {
        KafkaMessageContext ctx = KafkaMessageContextBuilder.Build(
            headers: ValidHeaders,
            partyId: "party-007",
            topic: "amendment-topic",
            partition: 1,
            offset: 200L,
            topicRole: "amendment");

        Assert.Equal("amendment", ctx.TopicRole);
    }
}
