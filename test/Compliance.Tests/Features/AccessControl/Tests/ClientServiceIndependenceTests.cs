using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ClientServiceIndependenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOriginalPreviewReceiptGivenFrozenIntentUnavailableOrCorrupt(bool corrupt)
    {
        // Arrange
        PreviewReadStore faults = null!;
        await using var host = new IndependenceHost(configureContent: inner => faults = new PreviewReadStore(inner));
        await host.SeedDraftRulesAsync();
        var evaluation = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 0))).IsSuccess);
        await IndependenceFacts.RunPreviewProcessAsync(host.Provider, host.Tenant, evaluation);
        var source = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant, evaluation));
        var admitted = source.AdmissionOf(evaluation)!;
        var decision = await host.HydrateAsync(new ClientIndependenceDecision(host.Tenant, evaluation));
        var original = decision.Evaluated!;
        var command = new MaterializeClientIndependencePreview(original.Operation.LogicalId,
            NativeFactReference.Of(source.Stream, admitted), decision.Stream, evaluation, 1, null, admitted.Intent, evaluation);
        faults.Blocked = admitted.Intent;
        faults.Corrupt = corrupt;

        // Act
        var replayed = await host.Bus.DispatchAsync(command, new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(NativeFactReference.Of(decision.Stream, original), Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value).Fact);
        Assert.Single(await host.EventsAsync<ClientIndependenceEvaluated>(decision.Stream));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldClassifyFrozenPreviewFailureGivenNoMaterializedReceipt(bool corrupt)
    {
        // Arrange
        PreviewReadStore faults = null!;
        await using var host = new IndependenceHost(configureContent: inner => faults = new PreviewReadStore(inner));
        await host.SeedDraftRulesAsync();
        var evaluation = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 0))).IsSuccess);
        var source = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant, evaluation));
        var admitted = source.AdmissionOf(evaluation)!;
        var target = new ClientIndependenceDecision(host.Tenant, evaluation).Stream;
        var command = new MaterializeClientIndependencePreview(Uuid.CreateVersion4(), NativeFactReference.Of(source.Stream, admitted),
            target, evaluation, 1, null, admitted.Intent, evaluation);
        faults.Blocked = admitted.Intent;
        faults.Corrupt = corrupt;

        // Act
        var refused = await host.Bus.DispatchAsync(command, new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Equal(!corrupt, refused.Error!.IsTransient);
        Assert.Empty(await host.EventsAsync<ClientIndependenceEvaluated>(target));
    }

    sealed class PreviewReadStore(Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore inner)
        : Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore
    {
        public Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef? Blocked { get; set; }
        public bool Corrupt { get; set; }
        public Task<Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id,
            string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference == Blocked)
                throw (Corrupt ? new Bdgrz.Compliance.Features.Content.Services.ImmutableContentIntegrityException("The frozen preview is corrupt.")
                    : new Bdgrz.Compliance.Features.Content.Services.ImmutableContentUnavailableException("The frozen preview is not yet available."));
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    [Fact]
    public async Task ShouldSealBrokerGlobalFrontiersGivenDifferentPhysicalAndGlobalPositions()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var evaluation = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new EvaluateClientIndependence(host.Tenant, evaluation, 1,
            new DateOnly(2026, 10, 7), 1))).IsSuccess);
        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant, evaluation));
        var intent = await Bdgrz.Compliance.Features.Content.Persistence.ImmutableContentStoreExtensions.ReadJsonAsync<ClientIndependencePreviewIntent>(
            host.Content, host.Tenant.ToString(), owner.AdmissionOf(evaluation)!.Intent);

        // Act
        var cuts = await Bdgrz.Compliance.Features.Content.Services.ImmutableSelectionManifests.ReadSelectionAsync<Bdgrz.Compliance.Features.Proofs.Models.AuthoritativeReadCut>(
            host.Content, host.Tenant.ToString(), intent.SourceCuts);

        // Assert
        Assert.Equal(2, cuts.Count);
        Assert.Equal(2, cuts.Select(cut => cut.AnchorSelector).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cuts, cut => Assert.Equal(EventStoreSourceFactResolver.GlobalBase + 1, cut.GlobalWatermarkExclusive));
        Assert.All(cuts, cut => Assert.NotEqual(1UL, cut.GlobalWatermarkExclusive));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayClientAdmissionGivenEqualWinnerBeforeLosingContentWriteUnavailable(bool preview)
    {
        // Arrange
        LosingClientStore content = null!;
        await using var host = new IndependenceHost(configureContent: inner => content = new LosingClientStore(inner,
            preview ? ClientIndependencePreviewIntent.Schema : IndependenceContent.ServiceSchema));
        await host.SeedDraftRulesAsync();
        var id = Uuid.CreateVersion4();
        var authoredService = IndependenceHost.Service();
        async Task<string> SendAsync()
        {
            if (preview)
            {
                var result = await host.SendAsync(new EvaluateClientIndependence(host.Tenant, id, 1, new DateOnly(2026, 10, 7), 0));
                Assert.True(result.IsSuccess, result.Error?.Message);
                return WireJson.Of(result.Value);
            }
            var service = await host.SendAsync(new RecordNonattestService(host.Tenant, id, 0, authoredService));
            Assert.True(service.IsSuccess, service.Error?.Message);
            return WireJson.Of(service.Value);
        }
        string? winner = null;
        content.Arm(async () => winner = await SendAsync());
        string? actual = null;

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await SendAsync());

        // Assert
        Assert.True(content.Failed);
        Assert.NotNull(winner);
        Assert.Null(failure);
        Assert.Equal(winner, actual);
        Assert.Single(await host.EventsAsync<DomainEvent>(new ClientServiceIndependence(host.Tenant).Stream));
    }

    sealed class LosingClientStore(Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore inner, string losingSchema)
        : Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore
    {
        Func<Task>? _admit;
        public bool Failed { get; private set; }
        public void Arm(Func<Task> admit) => _admit = admit;
        public async Task<Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id,
            string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Failed && _admit is { } admit && schema == losingSchema)
            {
                Failed = true;
                await admit();
                throw new Bdgrz.Compliance.Features.Content.Services.ImmutableContentUnavailableException("The losing client candidate publication is unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    [Fact]
    public async Task ShouldKeepBoundedClientAuthorityGivenManyServicesAndEvaluationAdmissions()
    {
        // Arrange
        await using var host = new IndependenceHost();
        await host.SeedDraftRulesAsync();
        for (var index = 0; index < 32; index++)
        {
            Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), index,
                IndependenceHost.Service()))).IsSuccess);
            Assert.True((await host.SendAsync(new EvaluateClientIndependence(host.Tenant, Uuid.CreateVersion4(), 1,
                new DateOnly(2026, 10, 7), index + 1))).IsSuccess);
        }

        // Act
        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var growing = typeof(ClientServiceIndependence).GetFields(global::System.Reflection.BindingFlags.Instance |
            global::System.Reflection.BindingFlags.NonPublic).Where(field => field.GetValue(owner) is
                global::System.Collections.ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(growing);
        Assert.Equal(32UL, owner.Epoch);
    }

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
        Assert.Equal(Enumerable.Range(1, count).Select(index => (ulong)index), (await ClientServiceNativeHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).Services.Select(item => item.Revision));
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
        var admissions = (await ClientServiceNativeHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).Admissions;
        Assert.Equal(105, admissions.Count);
        Assert.All(admissions, admission => Assert.Equal(1, admission.Revision));
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
        var content = new Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef(Uuid.CreateVersion4(),
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
