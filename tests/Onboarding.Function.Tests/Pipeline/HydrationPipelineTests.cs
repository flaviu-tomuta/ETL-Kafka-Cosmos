using Microsoft.Extensions.DependencyInjection;
using Onboarding.Function.DependencyInjection;
using Onboarding.Function.Pipeline;
using Onboarding.Function.Pipeline.Steps;
using Shared.Models.Contracts;
using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Onboarding.Function.Tests.Pipeline;

public sealed class HydrationPipelineTests
{
    private static HydrationContext BuildContext(string topicRole = "onboarding") =>
        new() { EntityId = "e1", MessageId = "m1", TopicRole = topicRole };

    // ─── AC1 + AC2: only applicable steps run; results have Applied = true ──

    [Fact]
    public async Task ExecuteAsync_AllStepsApply_AllResultsHaveAppliedTrue()
    {
        FakeStep step1 = new("Step1", appliesTo: true);
        FakeStep step2 = new("Step2", appliesTo: true);
        HydrationPipeline sut = new([step1, step2]);

        IEnumerable<EnrichmentResult> results = await sut.ExecuteAsync(BuildContext());

        List<EnrichmentResult> list = results.ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.True(r.Applied));
    }

    [Fact]
    public async Task ExecuteAsync_TwoApplicableSteps_EachResultCarriesCorrectStepName()
    {
        FakeStep step1 = new("AddressEnrichment", appliesTo: true);
        FakeStep step2 = new("ComplianceCheck", appliesTo: true);
        HydrationPipeline sut = new([step1, step2]);

        IEnumerable<EnrichmentResult> results = await sut.ExecuteAsync(BuildContext());

        List<string> stepNames = results.Select(r => r.StepName).ToList();
        Assert.Contains("AddressEnrichment", stepNames);
        Assert.Contains("ComplianceCheck", stepNames);
    }

    // ─── AC3: skipped step has Applied = false, Duration = TimeSpan.Zero ────

    [Fact]
    public async Task ExecuteAsync_OneStepNotApplicable_SkippedResultHasAppliedFalseAndZeroDuration()
    {
        FakeStep applicable = new("Applicable", appliesTo: true);
        FakeStep skipped = new("Skipped", appliesTo: false);
        HydrationPipeline sut = new([applicable, skipped]);

        IEnumerable<EnrichmentResult> results = await sut.ExecuteAsync(BuildContext());

        EnrichmentResult skippedResult = results.Single(r => r.StepName == "Skipped");
        Assert.False(skippedResult.Applied);
        Assert.Equal(TimeSpan.Zero, skippedResult.Duration);
    }

    [Fact]
    public async Task ExecuteAsync_MixedApplicability_ResultsCombineAppliedAndSkipped()
    {
        FakeStep step1 = new("Step1", appliesTo: true);
        FakeStep step2 = new("Step2", appliesTo: false);
        FakeStep step3 = new("Step3", appliesTo: true);
        HydrationPipeline sut = new([step1, step2, step3]);

        IEnumerable<EnrichmentResult> results = await sut.ExecuteAsync(BuildContext());

        List<EnrichmentResult> list = results.ToList();
        Assert.Equal(3, list.Count);
        Assert.True(list.Single(r => r.StepName == "Step1").Applied);
        Assert.False(list.Single(r => r.StepName == "Step2").Applied);
        Assert.True(list.Single(r => r.StepName == "Step3").Applied);
    }

    [Fact]
    public async Task ExecuteAsync_NoStepsApply_AllResultsSkipped()
    {
        FakeStep step1 = new("Step1", appliesTo: false);
        FakeStep step2 = new("Step2", appliesTo: false);
        HydrationPipeline sut = new([step1, step2]);

        IEnumerable<EnrichmentResult> results = await sut.ExecuteAsync(BuildContext());

        Assert.All(results, r => Assert.False(r.Applied));
    }

    // ─── AC4: EnrichmentStepException bubbles up ─────────────────────────────

    [Fact]
    public async Task ExecuteAsync_StepThrowsEnrichmentStepException_BubblesUpToCaller()
    {
        FakeStep throwing = new("ThrowingStep", appliesTo: true,
            throws: new EnrichmentStepException("fail", "e1", "m1", "ThrowingStep"));
        HydrationPipeline sut = new([throwing]);

        await Assert.ThrowsAsync<EnrichmentStepException>(() =>
            sut.ExecuteAsync(BuildContext()));
    }

    // ─── Concrete step stubs: AppliesTo always returns true ──────────────────

    [Fact]
    public void AddressEnrichmentStep_AppliesTo_ReturnsTrue()
    {
        AddressEnrichmentStep step = new();
        Assert.True(step.AppliesTo(BuildContext()));
    }

    [Fact]
    public void CreditCheckEnrichmentStep_AppliesTo_ReturnsTrue()
    {
        CreditCheckEnrichmentStep step = new();
        Assert.True(step.AppliesTo(BuildContext()));
    }

    [Fact]
    public void ComplianceEnrichmentStep_AppliesTo_ReturnsTrue()
    {
        ComplianceEnrichmentStep step = new();
        Assert.True(step.AppliesTo(BuildContext()));
    }

    // ─── DI registration ─────────────────────────────────────────────────────

    [Fact]
    public void AddOnboardingServices_RegistersAllThreeEnrichmentSteps()
    {
        ServiceCollection services = new();
        services.AddOnboardingServices();
        ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IEnrichmentStep> steps = provider.GetServices<IEnrichmentStep>();

        Assert.Equal(3, steps.Count());
    }

    [Fact]
    public void AddOnboardingServices_RegistersIHydrationPipeline()
    {
        ServiceCollection services = new();
        services.AddOnboardingServices();
        ServiceProvider provider = services.BuildServiceProvider();

        IHydrationPipeline pipeline = provider.GetRequiredService<IHydrationPipeline>();

        Assert.NotNull(pipeline);
    }

    // ─── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeStep : IEnrichmentStep
    {
        private readonly bool _appliesTo;
        private readonly Exception? _throws;

        public string StepName { get; }

        public FakeStep(string stepName, bool appliesTo, Exception? throws = null)
        {
            StepName = stepName;
            _appliesTo = appliesTo;
            _throws = throws;
        }

        public bool AppliesTo(HydrationContext context) => _appliesTo;

        public Task<EnrichmentResult> EnrichAsync(HydrationContext context)
        {
            if (_throws is not null) throw _throws;
            return Task.FromResult(new EnrichmentResult { StepName = StepName, Applied = true });
        }
    }
}
