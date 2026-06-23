using Microsoft.Extensions.DependencyInjection;
using Onboarding.Function.DependencyInjection;
using Onboarding.Function.Pipeline;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Onboarding.Function.Tests.Pipeline;

public sealed class OnboardingPipelineTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static KafkaMessageContext BuildContext(
        string partyId = "party-1",
        string messageId = "msg-1",
        string topicRole = "onboarding",
        string topic = "topic-a",
        int partition = 0,
        long offset = 42) =>
        new()
        {
            PartyId    = partyId,
            MessageId  = messageId,
            TopicRole  = topicRole,
            Topic      = topic,
            Partition  = partition,
            Offset     = offset,
            ReceivedAt = DateTimeOffset.UtcNow
        };

    private static string BuildPayload(string partyId = "party-1", int version = 5) =>
        $"{{\"partyId\":\"{partyId}\",\"version\":{version}}}";

    private static EnrichedCustomer BuildEntity(string partyId = "party-1") =>
        new() { Id = partyId, PartyId = partyId };

    // ─── AC1 (happy path): all four services called in order ─────────────────

    [Fact]
    public async Task ProcessAsync_HappyPath_CallsAllFourServicesInOrder()
    {
        List<string> callOrder = [];

        FakeHydrationPipeline hydration = new(
            onExecute: _ =>
            {
                callOrder.Add("hydration");
                return [new EnrichmentResult { StepName = "AddressStep", Applied = true, Duration = TimeSpan.FromMilliseconds(10) }];
            });

        FakeOutputAssembler assembler = new(
            onAssemble: (_, _) =>
            {
                callOrder.Add("assembler");
                return BuildEntity();
            });

        FakeCosmosWriter writer = new(
            onWrite: _ => callOrder.Add("writer"));

        FakeAuditService audit = new(
            onFlush: _ => callOrder.Add("audit"));

        OnboardingPipeline sut = new(hydration, assembler, writer, audit);

        await sut.ProcessAsync(BuildContext(), BuildPayload());

        Assert.Equal(["hydration", "assembler", "writer", "audit"], callOrder);
    }

    // ─── AC2: HydrationContext has correct fields from context + payload ──────

    [Fact]
    public async Task ProcessAsync_BuildsHydrationContextWithEntityIdFromPartyId()
    {
        HydrationContext? capturedContext = null;

        FakeHydrationPipeline hydration = new(
            onExecute: ctx =>
            {
                capturedContext = ctx;
                return [];
            });

        OnboardingPipeline sut = new(
            hydration,
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            new FakeAuditService());

        await sut.ProcessAsync(BuildContext(partyId: "party-xyz"), BuildPayload(partyId: "party-xyz", version: 7));

        Assert.NotNull(capturedContext);
        Assert.Equal("party-xyz", capturedContext!.EntityId);
    }

    [Fact]
    public async Task ProcessAsync_BuildsHydrationContextWithIncomingVersionFromPayload()
    {
        HydrationContext? capturedContext = null;

        FakeHydrationPipeline hydration = new(
            onExecute: ctx =>
            {
                capturedContext = ctx;
                return [];
            });

        OnboardingPipeline sut = new(
            hydration,
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            new FakeAuditService());

        await sut.ProcessAsync(BuildContext(), BuildPayload(version: 13));

        Assert.NotNull(capturedContext);
        Assert.Equal(13, capturedContext!.IncomingVersion);
    }

    [Fact]
    public async Task ProcessAsync_BuildsHydrationContextWithStoredVersionZero()
    {
        HydrationContext? capturedContext = null;

        FakeHydrationPipeline hydration = new(
            onExecute: ctx =>
            {
                capturedContext = ctx;
                return [];
            });

        OnboardingPipeline sut = new(
            hydration,
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            new FakeAuditService());

        await sut.ProcessAsync(BuildContext(), BuildPayload());

        Assert.NotNull(capturedContext);
        Assert.Equal(0, capturedContext!.StoredVersion);
    }

    [Fact]
    public async Task ProcessAsync_PayloadMissingVersionField_UsesDefaultVersionZero()
    {
        HydrationContext? capturedContext = null;

        FakeHydrationPipeline hydration = new(
            onExecute: ctx =>
            {
                capturedContext = ctx;
                return [];
            });

        OnboardingPipeline sut = new(
            hydration,
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            new FakeAuditService());

        await sut.ProcessAsync(BuildContext(), "{\"partyId\":\"x\"}");

        Assert.NotNull(capturedContext);
        Assert.Equal(0, capturedContext!.IncomingVersion);
    }

    // ─── AC3: CosmosWriter receives the assembled entity from OutputAssembler ─

    [Fact]
    public async Task ProcessAsync_PassesAssembledEntityToCosmosWriter()
    {
        EnrichedCustomer expectedEntity = BuildEntity("assembled-party");
        EnrichedCustomer? writtenEntity = null;

        FakeOutputAssembler assembler = new(
            onAssemble: (_, _) => expectedEntity);

        FakeCosmosWriter writer = new(
            onWrite: e => writtenEntity = e);

        OnboardingPipeline sut = new(
            new FakeHydrationPipeline(onExecute: _ => []),
            assembler,
            writer,
            new FakeAuditService());

        await sut.ProcessAsync(BuildContext(), BuildPayload());

        Assert.Same(expectedEntity, writtenEntity);
    }

    // ─── AC4: AuditService.FlushAsync called with correct AuditRecord fields ──

    [Fact]
    public async Task ProcessAsync_FlushesAuditRecordWithCorrectMessageId()
    {
        AuditRecord? capturedRecord = null;

        FakeAuditService audit = new(
            onFlush: r => capturedRecord = r);

        OnboardingPipeline sut = new(
            new FakeHydrationPipeline(onExecute: _ => []),
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            audit);

        await sut.ProcessAsync(BuildContext(messageId: "msg-99"), BuildPayload());

        Assert.NotNull(capturedRecord);
        Assert.Equal("msg-99", capturedRecord!.MessageId);
    }

    [Fact]
    public async Task ProcessAsync_FlushesAuditRecordWithCorrectPartyId()
    {
        AuditRecord? capturedRecord = null;

        FakeAuditService audit = new(
            onFlush: r => capturedRecord = r);

        OnboardingPipeline sut = new(
            new FakeHydrationPipeline(onExecute: _ => []),
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            audit);

        await sut.ProcessAsync(BuildContext(partyId: "party-007"), BuildPayload());

        Assert.NotNull(capturedRecord);
        Assert.Equal("party-007", capturedRecord!.PartyId);
    }

    [Fact]
    public async Task ProcessAsync_FlushesAuditRecordWithOutcomeSuccess()
    {
        AuditRecord? capturedRecord = null;

        FakeAuditService audit = new(
            onFlush: r => capturedRecord = r);

        OnboardingPipeline sut = new(
            new FakeHydrationPipeline(onExecute: _ => []),
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            audit);

        await sut.ProcessAsync(BuildContext(), BuildPayload());

        Assert.NotNull(capturedRecord);
        Assert.Equal("success", capturedRecord!.Outcome);
    }

    [Fact]
    public async Task ProcessAsync_FlushesAuditRecordWithCorrectTopicRole()
    {
        AuditRecord? capturedRecord = null;

        FakeAuditService audit = new(
            onFlush: r => capturedRecord = r);

        OnboardingPipeline sut = new(
            new FakeHydrationPipeline(onExecute: _ => []),
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            new FakeCosmosWriter(),
            audit);

        await sut.ProcessAsync(BuildContext(topicRole: "onboarding"), BuildPayload());

        Assert.NotNull(capturedRecord);
        Assert.Equal("onboarding", capturedRecord!.TopicRole);
    }

    // ─── AC5 (failure path): if writer throws, audit is NOT called ────────────

    [Fact]
    public async Task ProcessAsync_WhenWriterThrows_AuditServiceIsNotCalled()
    {
        bool auditCalled = false;

        FakeCosmosWriter writer = new(
            onWrite: _ => throw new InvalidOperationException("cosmos down"));

        FakeAuditService audit = new(
            onFlush: _ => auditCalled = true);

        OnboardingPipeline sut = new(
            new FakeHydrationPipeline(onExecute: _ => []),
            new FakeOutputAssembler(onAssemble: (_, _) => BuildEntity()),
            writer,
            audit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProcessAsync(BuildContext(), BuildPayload()));

        Assert.False(auditCalled, "AuditService.FlushAsync must not be called when writer throws");
    }

    // ─── DI registration ─────────────────────────────────────────────────────

    [Fact]
    public void AddOnboardingServices_RegistersIOnboardingCosmosWriter()
    {
        ServiceCollection services = new();
        services.AddOnboardingServices();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IOnboardingCosmosWriter));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(OnboardingCosmosWriter), descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddOnboardingServices_RegistersIOnboardingPipeline()
    {
        ServiceCollection services = new();
        services.AddOnboardingServices();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IOnboardingPipeline));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(OnboardingPipeline), descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    // ─── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeHydrationPipeline : IHydrationPipeline
    {
        private readonly Func<HydrationContext, IEnumerable<EnrichmentResult>> _onExecute;

        public FakeHydrationPipeline(
            Func<HydrationContext, IEnumerable<EnrichmentResult>>? onExecute = null)
        {
            _onExecute = onExecute ?? (_ => []);
        }

        public Task<IEnumerable<EnrichmentResult>> ExecuteAsync(HydrationContext context) =>
            Task.FromResult(_onExecute(context));
    }

    private sealed class FakeOutputAssembler : IOutputAssembler
    {
        private readonly Func<HydrationContext, IEnumerable<EnrichmentResult>, EnrichedCustomer> _onAssemble;

        public FakeOutputAssembler(
            Func<HydrationContext, IEnumerable<EnrichmentResult>, EnrichedCustomer>? onAssemble = null)
        {
            _onAssemble = onAssemble ?? ((_, _) => new EnrichedCustomer());
        }

        public EnrichedCustomer Assemble(HydrationContext context, IEnumerable<EnrichmentResult> results) =>
            _onAssemble(context, results);
    }

    private sealed class FakeCosmosWriter : IOnboardingCosmosWriter
    {
        private readonly Action<EnrichedCustomer>? _onWrite;

        public FakeCosmosWriter(Action<EnrichedCustomer>? onWrite = null)
        {
            _onWrite = onWrite;
        }

        public Task WriteAsync(EnrichedCustomer entity)
        {
            _onWrite?.Invoke(entity);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditService : IAuditService
    {
        private readonly Action<AuditRecord>? _onFlush;

        public FakeAuditService(Action<AuditRecord>? onFlush = null)
        {
            _onFlush = onFlush;
        }

        public Task FlushAsync(AuditRecord record)
        {
            _onFlush?.Invoke(record);
            return Task.CompletedTask;
        }
    }
}
