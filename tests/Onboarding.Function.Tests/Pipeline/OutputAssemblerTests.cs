using Onboarding.Function.Pipeline;
using Shared.Models.Models;

namespace Onboarding.Function.Tests.Pipeline;

public sealed class OutputAssemblerTests
{
    private readonly OutputAssembler _sut = new();

    private static HydrationContext BuildContext(
        string entityId = "party-1",
        string messageId = "msg-1",
        string topicRole = "onboarding",
        int incomingVersion = 2,
        int storedVersion = 1,
        bool wasApiFallback = false) => new()
    {
        EntityId        = entityId,
        MessageId       = messageId,
        TopicRole       = topicRole,
        IncomingVersion = incomingVersion,
        StoredVersion   = storedVersion,
        WasApiFallback  = wasApiFallback
    };

    // AC1: Id and PartyId both set to context.EntityId
    [Fact]
    public void Assemble_Sets_Id_And_PartyId_From_EntityId()
    {
        HydrationContext context = BuildContext(entityId: "party-42");

        EnrichedCustomer result = _sut.Assemble(context, []);

        Assert.Equal("party-42", result.Id);
        Assert.Equal("party-42", result.PartyId);
    }

    // AC1 (extended): Version set from IncomingVersion
    [Fact]
    public void Assemble_Sets_Version_From_IncomingVersion()
    {
        HydrationContext context = BuildContext(incomingVersion: 7);

        EnrichedCustomer result = _sut.Assemble(context, []);

        Assert.Equal(7, result.Version);
    }

    // AC2: Only Applied=true steps appear in LastHydrationSteps
    [Fact]
    public void Assemble_LastHydrationSteps_Contains_Only_Applied_Steps()
    {
        HydrationContext context = BuildContext();
        EnrichmentResult[] results =
        [
            new() { StepName = "AddressEnrichment",     Applied = true,  Duration = TimeSpan.FromMilliseconds(80) },
            new() { StepName = "CreditCheckEnrichment", Applied = false, Duration = TimeSpan.Zero },
            new() { StepName = "ComplianceEnrichment",  Applied = true,  Duration = TimeSpan.FromMilliseconds(62) }
        ];

        EnrichedCustomer assembled = _sut.Assemble(context, results);

        Assert.Equal(["AddressEnrichment", "ComplianceEnrichment"], assembled.AuditSummary!.LastHydrationSteps);
    }

    // AC2: Unapplied step name does not appear in LastHydrationSteps
    [Fact]
    public void Assemble_Excludes_Unapplied_Steps_From_LastHydrationSteps()
    {
        HydrationContext context = BuildContext();
        EnrichmentResult[] results =
        [
            new() { StepName = "CreditCheckEnrichment", Applied = false, Duration = TimeSpan.Zero }
        ];

        EnrichedCustomer assembled = _sut.Assemble(context, results);

        Assert.Empty(assembled.AuditSummary!.LastHydrationSteps);
    }

    // AC3: WasApiFallback propagated when true
    [Fact]
    public void Assemble_Sets_WasApiFallback_True_When_Context_Has_ApiFallback()
    {
        HydrationContext context = BuildContext(wasApiFallback: true);

        EnrichedCustomer assembled = _sut.Assemble(context, []);

        Assert.True(assembled.AuditSummary!.WasApiFallback);
    }

    // AC3: WasApiFallback false when context is false
    [Fact]
    public void Assemble_Sets_WasApiFallback_False_When_Context_Has_No_ApiFallback()
    {
        HydrationContext context = BuildContext(wasApiFallback: false);

        EnrichedCustomer assembled = _sut.Assemble(context, []);

        Assert.False(assembled.AuditSummary!.WasApiFallback);
    }

    // AC4: All applied steps (by count and name) appear in LastHydrationSteps — no step omitted
    [Fact]
    public void Assemble_LastHydrationSteps_Includes_All_Applied_Results_With_NonZero_Duration()
    {
        HydrationContext context = BuildContext();
        EnrichmentResult[] results =
        [
            new() { StepName = "Step-A", Applied = true, Duration = TimeSpan.FromMilliseconds(50) },
            new() { StepName = "Step-B", Applied = true, Duration = TimeSpan.FromMilliseconds(75) },
            new() { StepName = "Step-C", Applied = true, Duration = TimeSpan.FromMilliseconds(25) }
        ];

        EnrichedCustomer assembled = _sut.Assemble(context, results);

        Assert.Equal(3, assembled.AuditSummary!.LastHydrationSteps.Count);
        Assert.Contains("Step-A", assembled.AuditSummary.LastHydrationSteps);
        Assert.Contains("Step-B", assembled.AuditSummary.LastHydrationSteps);
        Assert.Contains("Step-C", assembled.AuditSummary.LastHydrationSteps);
    }

    // Technical: LastMessageId set from context.MessageId
    [Fact]
    public void Assemble_Sets_LastMessageId_From_Context_MessageId()
    {
        HydrationContext context = BuildContext(messageId: "msg-abc-999");

        EnrichedCustomer assembled = _sut.Assemble(context, []);

        Assert.Equal("msg-abc-999", assembled.AuditSummary!.LastMessageId);
    }

    // Technical: LastUpdatedBy = "onboarding-app" when TopicRole is "onboarding"
    [Fact]
    public void Assemble_Sets_LastUpdatedBy_OnboardingApp_For_Onboarding_Role()
    {
        HydrationContext context = BuildContext(topicRole: "onboarding");

        EnrichedCustomer assembled = _sut.Assemble(context, []);

        Assert.Equal("onboarding-app", assembled.LastUpdatedBy);
    }

    // Technical: LastUpdatedBy = "amendment-app" when TopicRole is not "onboarding"
    [Fact]
    public void Assemble_Sets_LastUpdatedBy_AmendmentApp_For_Non_Onboarding_Role()
    {
        HydrationContext context = BuildContext(topicRole: "amendment");

        EnrichedCustomer assembled = _sut.Assemble(context, []);

        Assert.Equal("amendment-app", assembled.LastUpdatedBy);
    }

    // Technical: ProcessedAt is set (non-default, >= before assembly)
    [Fact]
    public void Assemble_Sets_ProcessedAt_To_UtcNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        HydrationContext context = BuildContext();

        EnrichedCustomer assembled = _sut.Assemble(context, []);

        Assert.True(assembled.AuditSummary!.ProcessedAt >= before);
    }
}
