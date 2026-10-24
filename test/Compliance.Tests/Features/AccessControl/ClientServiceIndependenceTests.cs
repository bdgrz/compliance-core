using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ClientServiceIndependenceTests
{
    [Fact]
    public async Task ShouldRetainCompleteHistoryGivenMoreServicesThanFormerCap()
    {
        // Arrange
        await using var host = new IndependenceHost();
        const int count = 130;

        // Act
        for (var index = 0; index < count; index++)
        {
            var recorded = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), index,
                IndependenceHost.Service(start: new DateOnly(2020, 1, 1).AddDays(index))));
            Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        }

        var history = await host.SendAsync(new GetClientIndependenceHistory(host.Tenant));
        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));

        // Assert
        Assert.True(history.IsSuccess, history.Error?.Message);
        Assert.Equal(count, history.Value!.Services.Count);
        Assert.Equal(count, history.Value.Sequence);
        Assert.Equal((ulong)count, owner.Epoch);
        Assert.Equal(Enumerable.Range(0, count).Select(index => new DateOnly(2020, 1, 1).AddDays(index)),
            history.Value.Services.Select(service => service.Content.StartedOn));
        Assert.Equal(Enumerable.Range(1, count).Select(index => (ulong)index), owner.Services.Select(item => item.Revision));
    }

    [Fact]
    public async Task ShouldAdmitEveryEvaluationWithoutAdvancingServiceEpochGivenMoreThanOneHundredEvaluations()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);

        // Act
        var admitted = new List<Uuid>();
        for (var index = 0; index < 105; index++)
        {
            var evaluation = Uuid.CreateVersion4();
            var result = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
                new DateOnly(2026, 10, 7), 1));
            Assert.True(result.IsSuccess, result.Error?.Message);
            admitted.Add(evaluation);
        }

        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var next = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service("readiness")));

        // Assert
        Assert.Equal(1UL, owner.Epoch);
        Assert.Equal(105, owner.Admissions.Count);
        Assert.All(owner.Admissions, admission => Assert.Equal(1, admission.Revision));
        Assert.True(next.IsSuccess, next.Error?.Message);
    }

    [Fact]
    public async Task ShouldReturnOriginalViewAndAppendNothingGivenExactServiceRetryAfterLaterServices()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var serviceId = Uuid.CreateVersion4();
        var content = IndependenceHost.Service();
        var request = new RecordNonattestService(host.Tenant, serviceId, 0, content);
        var first = await host.SendAsync(request);
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service("readiness")))).IsSuccess);
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retried = await host.SendAsync(request with
        {
            Content = content with
            { FirmStaffMemberIds = content.FirmStaffMemberIds.ToArray() }
        });

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(IndependenceHost.Json(first.Value), IndependenceHost.Json(retried.Value));
        Assert.Equal(serviceId, retried.Value!.ServiceRecordId);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var fact = (await host.EventsAsync<NonattestServiceRecorded>(stream)).First();
        Assert.Equal(1UL, fact.Revision);
        Assert.Equal(200, (await FirstResponses.ReplayAsync(host.Content, host.Tenant.ToString(), fact.FirstResponse!))
            .HttpStatus);
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenSameServiceIdentityWithChangedIntentActorOrEpoch()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var serviceId = Uuid.CreateVersion4();
        var content = IndependenceHost.Service();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, serviceId, 0, content))).IsSuccess);
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var otherContent = await host.SendAsync(new RecordNonattestService(host.Tenant, serviceId, 0,
            content with { SourceReference = "A different source" }));
        var otherActor = await host.SendAsync(new RecordNonattestService(host.Tenant, serviceId, 0, content),
            IndependenceHost.ActorFor(Uuid.CreateVersion4()));
        var otherEpoch = await host.SendAsync(new RecordNonattestService(host.Tenant, serviceId, 1, content));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherContent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherEpoch.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenStaleEpochEmptyIdentityNegativeEpochOrInvalidContent()
    {
        // Arrange
        await using var host = new IndependenceHost();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var stale = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()));
        var future = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 5,
            IndependenceHost.Service()));
        var empty = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.Empty, 1,
            IndependenceHost.Service()));
        var negative = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), -1,
            IndependenceHost.Service()));
        var invalid = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service() with { FirmStaffMemberIds = [] }));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, future.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, empty.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, negative.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRecordOneServiceGivenConcurrentEqualRequestsOnTheSameEpoch()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var serviceId = Uuid.CreateVersion4();
        var request = new RecordNonattestService(host.Tenant, serviceId, 0, IndependenceHost.Service());

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var scope = host.Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request, host.Actor);
        }));

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Single(results.Select(result => IndependenceHost.Json(result.Value)).Distinct());
        Assert.Single(await host.EventsAsync<NonattestServiceRecorded>(new ClientServiceIndependence(host.Tenant).Stream));
    }

    [Fact]
    public async Task ShouldMaterializeFromFrozenSnapshotOnlyGivenLaterServicesAndRuleChanges()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        var firstService = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service("readiness", new DateOnly(2026, 1, 1))));
        Assert.True(firstService.IsSuccess);
        var evaluation = Uuid.CreateVersion4();
        var admitted = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 1));
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);

        // Act: an impairing service and a changed draft rule arrive before the process runs.
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service("design", new DateOnly(2026, 2, 1))))).IsSuccess);
        await host.SeedDraftRulesAsync(IndependenceHost.DefaultRules with { LookBackMonths = 48 });
        var run = await host.RunPreviewAsync(evaluation);
        var history = await host.SendAsync(new GetClientIndependenceHistory(host.Tenant));
        var decision = await host.HydrateAsync(new ClientIndependenceDecision(host.Tenant, evaluation));

        // Assert
        Assert.Equal(ProcessState.Completed, run.State);
        var preview = Assert.Single(history.Value!.Evaluations);
        Assert.Equal("partner_evaluation_required", preview.DecisionCode);
        Assert.Equal("conditionally_compatible", preview.Outcome);
        Assert.Equal(firstService.Value!.ServiceRecordId, Assert.Single(preview.CompleteServiceHistory).ServiceRecordId);
        Assert.Equal(1, preview.RuleSetVersion);
        Assert.Equal(12, preview.EvaluatedRules.Content.LookBackMonths);
        Assert.True(preview.ProductionAcceptanceBlocked);
        Assert.Equal(host.MemberReference.Id, preview.Actor.Id);
        Assert.Equal(2, history.Value.Services.Count);
        Assert.Equal(1UL, decision.Evaluated!.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalAcknowledgmentGivenExactEvaluateRetryAfterLaterServiceAndRuleChange()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        var evaluation = Uuid.CreateVersion4();
        var request = new EvaluateClientIndependence(host.Tenant, evaluation, 1, new DateOnly(2026, 10, 7), 0);
        var first = await host.SendAsync(request);
        await host.RunPreviewAsync(evaluation);
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        await host.SeedDraftRulesAsync(IndependenceHost.DefaultRules with { LookBackMonths = 24 });
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retried = await host.SendAsync(request);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, retried.Value);
        Assert.Equal(evaluation, retried.Value!.OperationId);
        Assert.Equal($"/api/v1/tenants/{host.Tenant}/operations/{evaluation}", retried.Value.StatusUrl);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var admitted = Assert.Single(await host.EventsAsync<ClientIndependenceEvaluationAdmitted>(stream));
        Assert.Equal(202, (await FirstResponses.ReplayAsync(host.Content, host.Tenant.ToString(), admitted.FirstResponse!))
            .HttpStatus);
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenSameEvaluationIdentityWithChangedIntentActorOrEpoch()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        var evaluation = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 0))).IsSuccess);
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var otherPeriod = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 8), 0));
        var otherActor = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 0), IndependenceHost.ActorFor(Uuid.CreateVersion4()));
        var otherEpoch = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherPeriod.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherEpoch.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectEvaluationWithoutAppendGivenStaleEpochStaleRuleOrInvalidInput()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var staleEpoch = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.CreateVersion4(), 1,
            new DateOnly(2026, 10, 7), 0));
        var staleRule = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.CreateVersion4(), 2,
            new DateOnly(2026, 10, 7), 1));
        var emptyIdentity = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.Empty, 1,
            new DateOnly(2026, 10, 7), 1));
        var noPeriod = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.CreateVersion4(), 1,
            DateOnly.MinValue, 1));
        var negative = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.CreateVersion4(), 1,
            new DateOnly(2026, 10, 7), -1));
        var unevaluable = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.CreateVersion4(), 1,
            new DateOnly(1, 1, 2), 1));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, unevaluable.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, staleEpoch.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, staleRule.Error?.Kind);
        Assert.True(staleRule.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Validation, emptyIdentity.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noPeriod.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, negative.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectEvaluationAdmissionGivenNoDraftRuleRetained()
    {
        // Arrange
        await using var host = new IndependenceHost();

        // Act
        var result = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.CreateVersion4(), 1,
            new DateOnly(2026, 10, 7), 0));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(await host.EventsAsync<DomainEvent>(new ClientServiceIndependence(host.Tenant).Stream));
    }

    [Fact]
    public async Task ShouldReplayOriginalFactAndAppendNothingGivenRedeliveredMaterializeReceiver()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        var evaluation = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 0))).IsSuccess);
        var record = await IndependenceFacts.StartPreviewProcessAsync(host.Provider, host.Tenant, evaluation);
        var command = new MaterializeClientIndependencePreview(Uuid.CreateVersion4(), record.Source,
            new ClientIndependenceDecision(host.Tenant, evaluation).Stream, evaluation, 1, null, record.Intent, evaluation);
        var system = new RequestDispatchContext(RequestActor.System);

        // Act
        var first = await host.Bus.DispatchAsync(command, system, CancellationToken.None);
        var replayed = await host.Bus.DispatchAsync(command, system, CancellationToken.None);
        var otherDelivery = await host.Bus.DispatchAsync(command with { CommandId = Uuid.CreateVersion4() }, system,
            CancellationToken.None);
        var otherIntent = await host.Bus.DispatchAsync(command with { Intent = record.Intent with { Sha256 = new string('0', 64) } },
            system, CancellationToken.None);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(first.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(otherDelivery.Value);
        Assert.False(otherIntent.IsSuccess);
        Assert.Single(await host.EventsAsync<ClientIndependenceEvaluated>(
            new ClientIndependenceDecision(host.Tenant, evaluation).Stream));
    }

    [Fact]
    public async Task ShouldRefuseMaterializeReceiverGivenNonSystemActorOrForeignSource()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        var evaluation = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 0))).IsSuccess);
        var record = await IndependenceFacts.StartPreviewProcessAsync(host.Provider, host.Tenant, evaluation);
        var decision = new ClientIndependenceDecision(host.Tenant, evaluation).Stream;
        var command = new MaterializeClientIndependencePreview(Uuid.CreateVersion4(), record.Source, decision,
            evaluation, 1, null, record.Intent, evaluation);

        // Act
        var member = await host.SendAsync(command);
        var otherTarget = await host.Bus.DispatchAsync(command with
        { Target = new ClientIndependenceDecision(host.Tenant, Uuid.CreateVersion4()).Stream },
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var otherSource = await host.Bus.DispatchAsync(command with
        { Source = record.Source with { EventId = Uuid.CreateVersion4() } },
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, member.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherTarget.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherSource.Error?.Kind);
        Assert.Empty(await host.EventsAsync<ClientIndependenceEvaluated>(decision));
    }

    [Fact]
    public async Task ShouldKeepEpochsIndependentGivenServicesRecordedForAnotherClient()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var other = Uuid.CreateVersion4();

        // Act
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service()))).IsSuccess);
        var foreign = await host.SendAsync(new RecordNonattestService(other, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()));

        // Assert
        Assert.True(foreign.IsSuccess, foreign.Error?.Message);
        Assert.Equal(2UL, (await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Epoch);
        Assert.Equal(1UL, (await host.HydrateAsync(new ClientServiceIndependence(other))).Epoch);
        Assert.NotEqual(new ClientServiceIndependence(host.Tenant).Stream, new ClientServiceIndependence(other).Stream);
    }

    [Fact]
    public void ShouldRefuseCrossTenantAndOutOfOrderReplayGivenForeignOrDuplicateServiceFacts()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var owner = new ClientServiceIndependence(tenant);
        var provenance = new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null,
            "RecordNonattestService", ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic"), owner.Stream,
            "initial", 0, new string('a', 64), IndependenceHost.At, owner.Stream, owner.Stream);
        var content = new Bdgrz.Compliance.Features.Proofs.ImmutableContentRef(Uuid.CreateVersion4(),
            new string('b', 64), 2, "application/json", "NonattestServiceRecorded.ServiceContent.v1");
        var logical = provenance with { RootOperationId = provenance.LogicalId };
        var valid = new NonattestServiceRecorded(tenant, logical.LogicalId, content, logical, 1, content, content);

        // Act
        var foreign = () => new AggregateScenario<ClientServiceIndependence>(owner).Given(
            valid with { TenantId = Uuid.CreateVersion4() });
        var skipped = () => new AggregateScenario<ClientServiceIndependence>(new ClientServiceIndependence(tenant)).Given(
            valid with { Revision = 2 });
        var duplicate = () => new AggregateScenario<ClientServiceIndependence>(new ClientServiceIndependence(tenant)).Given(
            valid, valid with { Revision = 2 });

        // Assert
        Assert.Throws<InvalidOperationException>(foreign);
        Assert.Throws<InvalidOperationException>(skipped);
        Assert.Throws<InvalidOperationException>(duplicate);
    }
}
