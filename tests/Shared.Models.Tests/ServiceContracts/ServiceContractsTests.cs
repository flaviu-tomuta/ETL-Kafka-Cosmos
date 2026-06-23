using Microsoft.Extensions.DependencyInjection;
using Shared.Models.Contracts;
using Shared.Models.DependencyInjection;
using Shared.Models.ErrorClassification;
using Shared.Models.Models;

namespace Shared.Models.Tests.ServiceContracts;

public sealed class ServiceContractsTests
{
    [Fact]
    public void ValidationResult_IsRejected_True_ReasonIsAccessible_IsNoOpIsFalse()
    {
        ValidationResult result = new() { IsRejected = true, Reason = "DuplicateRecord", IsNoOp = false };

        Assert.True(result.IsRejected);
        Assert.Equal("DuplicateRecord", result.Reason);
        Assert.False(result.IsNoOp);
    }

    [Fact]
    public void ValidationResult_IsNoOp_True_IsRejectedDefaultsFalse_ReasonNull()
    {
        ValidationResult result = new() { IsNoOp = true };

        Assert.True(result.IsNoOp);
        Assert.False(result.IsRejected);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void ValidationResult_Reason_CanBeNull()
    {
        ValidationResult result = new() { IsRejected = false, IsNoOp = false, Reason = null };

        Assert.Null(result.Reason);
    }

    [Fact]
    public void AmendmentResult_IsNoOp_True_EntityIsNull()
    {
        AmendmentResult result = new() { IsNoOp = true };

        Assert.True(result.IsNoOp);
        Assert.Null(result.Entity);
    }

    [Fact]
    public void AmendmentResult_Entity_CanHoldEnrichedCustomer()
    {
        EnrichedCustomer customer = new() { Id = "12345", PartyId = "12345" };
        AmendmentResult result = new() { IsNoOp = false, Entity = customer };

        Assert.False(result.IsNoOp);
        Assert.NotNull(result.Entity);
        Assert.Equal("12345", result.Entity.PartyId);
    }

    [Fact]
    public void AddSharedServices_RegistersIErrorClassifier_AsSingleton()
    {
        ServiceCollection services = new();
        services.AddSharedServices();
        ServiceProvider provider = services.BuildServiceProvider();

        IErrorClassifier instance1 = provider.GetRequiredService<IErrorClassifier>();
        IErrorClassifier instance2 = provider.GetRequiredService<IErrorClassifier>();

        Assert.NotNull(instance1);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddSharedServices_ResolvedIErrorClassifier_CanClassifyExceptions()
    {
        ServiceCollection services = new();
        services.AddSharedServices();
        ServiceProvider provider = services.BuildServiceProvider();

        IErrorClassifier classifier = provider.GetRequiredService<IErrorClassifier>();
        ErrorCategory category = classifier.Classify(new TimeoutException());

        Assert.Equal(ErrorCategory.Transient, category);
    }

    [Fact]
    public void AddSharedServices_MultipleIEnrichmentSteps_AllResolved()
    {
        ServiceCollection services = new();
        services.AddSharedServices();
        services.AddScoped<IEnrichmentStep, TestEnrichmentStepA>();
        services.AddScoped<IEnrichmentStep, TestEnrichmentStepB>();
        ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IEnrichmentStep> steps = provider.GetServices<IEnrichmentStep>();

        Assert.Equal(2, steps.Count());
    }

    [Fact]
    public void AddSharedServices_MultipleIOperationHandlers_AllResolved()
    {
        ServiceCollection services = new();
        services.AddSharedServices();
        services.AddScoped<IOperationHandler, TestAddOperationHandler>();
        services.AddScoped<IOperationHandler, TestRemoveOperationHandler>();
        ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IOperationHandler> handlers = provider.GetServices<IOperationHandler>();

        Assert.Equal(2, handlers.Count());
    }

    private sealed class TestEnrichmentStepA : IEnrichmentStep
    {
        public bool AppliesTo(HydrationContext context) => true;
        public Task<EnrichmentResult> EnrichAsync(HydrationContext context) =>
            Task.FromResult(new EnrichmentResult { StepName = "TestA", Applied = true });
    }

    private sealed class TestEnrichmentStepB : IEnrichmentStep
    {
        public bool AppliesTo(HydrationContext context) => true;
        public Task<EnrichmentResult> EnrichAsync(HydrationContext context) =>
            Task.FromResult(new EnrichmentResult { StepName = "TestB", Applied = true });
    }

    private sealed class TestAddOperationHandler : IOperationHandler
    {
        public string OperationType => "add";
        public ValidationResult Validate(Operation operation, EnrichedCustomer storedEntity) =>
            new() { IsRejected = false, IsNoOp = false };
        public EnrichedCustomer Apply(Operation operation, EnrichedCustomer storedEntity) =>
            storedEntity;
    }

    private sealed class TestRemoveOperationHandler : IOperationHandler
    {
        public string OperationType => "remove";
        public ValidationResult Validate(Operation operation, EnrichedCustomer storedEntity) =>
            new() { IsRejected = false, IsNoOp = false };
        public EnrichedCustomer Apply(Operation operation, EnrichedCustomer storedEntity) =>
            storedEntity;
    }
}
