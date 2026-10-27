using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Reactors;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The full composition over in-memory stores for the application import owners: handlers are built from the
///     same scope as the production ones, the process record is created from the sealed plan the way the root reactor
///     does, and the cancellation reactor is driven from the committed cancellation fact.
/// </summary>
sealed class ApplicationImportHost : IAsyncDisposable
{
    public static readonly DateTimeOffset StartedAt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    readonly ServiceProvider _provider;

    public ApplicationImportHost(Func<IProcessRecordStore, IProcessRecordStore>? store = null)
    {
        var services = new ServiceCollection();
        var values = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        };
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.RemoveAll<ISourceFactResolver>();
        services.AddSingleton<ISourceFactResolver>(new EventStoreSourceFactResolver(events));
        services.AddSingleton<IAuthoritativeSnapshotReader>(new EventStoreSnapshotReader(events));
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IPlatformUserDirectoryReader>(new ExistingUsers());
        Clock = new ManualClock(StartedAt);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<ITenantActivity>(Activity);
        services.RemoveAll<IApplicationImportRetirementImpactReader>();
        services.AddSingleton<IApplicationImportRetirementImpactReader>(Impacts);
        services.RemoveAll<IApplicationChangeImpactReader>();
        services.AddSingleton<IApplicationChangeImpactReader>(Impacts);
        if (store is not null)
        {
            services.RemoveAll<IProcessRecordStore>();
            services.AddSingleton<IProcessRecordStore>(provider =>
                store(new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>())));
        }

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Scope = _provider.CreateAsyncScope();
        Actor = Personal(ActorUserId);
    }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public Uuid ActorUserId { get; } = Uuid.CreateVersion4();

    public Uuid ActorMemberId => RbacIds.Member(TenantId, ActorUserId);

    public ManualClock Clock { get; }

    public SwitchableTenantActivity Activity { get; } = new();

    public FixedRetirementImpacts Impacts { get; } = new();

    public ClaimsPrincipal Actor { get; }

    public AsyncServiceScope Scope { get; }

    public IServiceProvider Services => Scope.ServiceProvider;

    public IServiceProvider Provider => _provider;

    public IAggregateReader Reader => Services.GetRequiredService<IAggregateReader>();

    public IAggregateWriter Writer => Services.GetRequiredService<IAggregateWriter>();

    public IDomainEventReader Events => Services.GetRequiredService<IDomainEventReader>();

    public IImmutableContentStore Content => Services.GetRequiredService<IImmutableContentStore>();

    public IRequestBus Bus => Services.GetRequiredService<IRequestBus>();

    public IProcessRecordStore Records => Services.GetRequiredService<IProcessRecordStore>();

    public ProcessDefinition Definition => Provider.GetRequiredService<ProcessCatalog>()
        .Get("ApplicationImportExecutionProcess");

    public StageApplicationImportHandler StageHandler => new(Services.GetRequiredService<NativeOperationExecutor>(),
        Reader, Content, Clock);

    public CorrelateApplicationImportRowHandler CorrelateHandler => new(
        Services.GetRequiredService<NativeOperationExecutor>(), Reader, Content, Clock, Events);

    public FreezeApplicationImportOmissionProposalHandler FreezeHandler => new(
        Services.GetRequiredService<NativeOperationExecutor>(), Reader, Content, Clock, Events);

    public AcceptApplicationImportHandler AcceptHandler => new(Services.GetRequiredService<NativeOperationExecutor>(),
        Reader, Content, Clock, Events, Impacts);

    public CancelApplicationImportHandler CancelHandler => new(Services.GetRequiredService<NativeOperationExecutor>(),
        Reader, Content, Clock);

    public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
        new RequestDispatchContext(Actor, new HttpInvocation("PUT", "/application-imports", "/application-imports",
            "application-import-test")));

    /// <summary>A principal that passes the personal HTTP actor check of the import handlers.</summary>
    public static ClaimsPrincipal Personal(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("name", "Import manager")],
        "BdgrzSession"));

    public ApplicationImportBatch NewBatch(Uuid batchId, Uuid selected = default) => new(TenantId, batchId, selected);

    public Task<T> HydrateAsync<T>(T aggregate) where T : Aggregate => Reader.HydrateAsync(aggregate).AsTask();

    public Task<ApplicationImportBatch> BatchAsync(Uuid batchId) => HydrateAsync(NewBatch(batchId));

    public Task<ApplicationImportBatchHistorySnapshot> BatchHistoryAsync(ApplicationImportBatch batch) =>
        ApplicationImportBatchHistory.ReadAsync(Events, batch);

    public Task<ApplicationImportSource> SourceAsync(string key = "manual", string ns = "applications",
        Uuid selectedBatchId = default) => HydrateAsync(new ApplicationImportSource(TenantId, key, ns,
            selectedBatchId: selectedBatchId));

    public Task<ApplicationImportSourceHistorySnapshot> SourceHistoryAsync(ApplicationImportSource source) =>
        ApplicationImportSourceHistory.ReadAsync(Events, source);

    /// <summary>The application as a fixed-cut read composes it: a batch publication shows before its targets settle.</summary>
    public async Task<DeclaredApplication> PublishedApplicationAsync(Uuid applicationId)
    {
        var cut = await Services.GetRequiredService<ApplicationPublicationReader>().BeginAsync();
        return await cut.ReadAsync(TenantId, applicationId);
    }

    public DeclaredApplication NewApplication(Uuid applicationId) => new(TenantId, applicationId);

    public Task<DeclaredApplication> ApplicationAsync(Uuid applicationId) =>
        HydrateAsync(new DeclaredApplication(TenantId, applicationId));

    public async Task<Result<ApplicationImportRegistration>> StageAsync(Uuid? submissionId = null,
        string coverage = "partial", string key = "manual", string ns = "applications",
        params ApplicationImportInputRow[] rows) => await StageHandler.HandleAsync(Context(
        new StageApplicationImport(TenantId, submissionId ?? Uuid.CreateVersion4(), key, ns, coverage,
            rows.Length == 0 ? [new("record-1", "Payroll", "Pay staff", null)] : rows)), CancellationToken.None);

    public async Task<ApplicationImportBatch> StageBatchAsync(string coverage = "partial", string key = "manual",
        string ns = "applications", params ApplicationImportInputRow[] rows)
    {
        var registration = await StageAsync(null, coverage, key, ns, rows);
        Assert.True(registration.IsSuccess, registration.Error?.Message);
        return await BatchAsync(registration.Value!.BatchId);
    }

    public async Task<IReadOnlyList<ApplicationImportStagedRow>> RowsAsync(ApplicationImportBatch batch) =>
        (await ApplicationImportContent.ReadAsync<ApplicationImportRowsManifest>(Content, TenantId,
            batch.Staged!.RowsManifest)).Rows;

    public Task<Result> CorrelateAsync(Uuid batchId, Uuid rowId, long expectedRevision, string decision,
        Uuid? applicationId = null, long? expectedApplicationRevision = null, Uuid? commandId = null) =>
        CorrelateHandler.HandleAsync(Context(new CorrelateApplicationImportRow(commandId ?? Uuid.CreateVersion4(),
            TenantId, batchId, rowId, expectedRevision, decision, applicationId, expectedApplicationRevision,
            "Reviewed identity")), CancellationToken.None).AsTask();

    public Task<Result<OperationAcceptedResponse>> AcceptAsync(Uuid batchId, long expectedRevision,
        Uuid? commandId = null) => AcceptHandler.HandleAsync(Context(new AcceptApplicationImport(
        commandId ?? Uuid.CreateVersion4(), TenantId, batchId, expectedRevision)), CancellationToken.None).AsTask();

    public Task<Result<OperationAcceptedResponse>> CancelAsync(Uuid batchId, long expectedRevision,
        Uuid? commandId = null) => CancelHandler.HandleAsync(Context(new CancelApplicationImport(
        commandId ?? Uuid.CreateVersion4(), TenantId, batchId, expectedRevision, "Superseded")),
        CancellationToken.None).AsTask();

    /// <summary>Creates (if absent) the process record of a sealed plan, as the root reactor does.</summary>
    public Task<ProcessRecord> StartProcessAsync(Uuid batchId) =>
        ApplicationImportProcessSupport.StartAsync(Services, TenantId, batchId);

    /// <summary>Starts (if absent) and drives the process of a sealed plan to its end.</summary>
    public Task<ProcessRecord> RunProcessAsync(Uuid batchId) =>
        ApplicationImportProcessSupport.RunAsync(Provider, Services, TenantId, batchId);

    /// <summary>The receiver command a step of the record builds, with the deterministic command id of the driver.</summary>
    public async Task<IRequest<ReceiverOutcome>?> StepAsync(ProcessRecord record, string stage, string receiver,
        string? target = null) => await Definition.CreateReceiver(new ProcessStep(record, stage, receiver, target,
        EffectCommandId(record, receiver, target)), Services, CancellationToken.None);

    public static Uuid EffectCommandId(ProcessRecord record, string receiver, string? target) =>
        Bdgrz.Compliance.Features.Kernel.EffectIdentity.CommandId(record.Source.EventId,
            $"{record.Key.ProcessKind}:{receiver}", target ?? "-", record.Episode);

    public ValueTask<Result<ReceiverOutcome>> SendAsync(IRequest<ReceiverOutcome> command) =>
        Bus.DispatchAsync(command, new RequestDispatchContext(RequestActor.System), CancellationToken.None);

    /// <summary>Runs the cancellation reactor on the committed cancellation fact of a batch, as the worker would.</summary>
    public async Task ReactCancellationAsync(Uuid batchId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var reactor = scope.ServiceProvider.GetRequiredService<ImportCancellationReactor>();
        await new ReactorScenario(new TenantId(TenantId.ToString())).RunAsync(reactor);
        var stream = NewBatch(batchId).Stream;
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            if (record.Event is ApplicationImportCancellationAccepted)
                await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<ApplicationImportCancellationAccepted>(
                    record), CancellationToken.None);
    }

    readonly Dictionary<string, ProjectionCheckpoint> _checkpoints = [];

    /// <summary>Projects every committed fact into the named projector workloads of the tenant, resuming from where each stopped.</summary>
    public async Task CatchUpAsync(params string[] workloads)
    {
        foreach (var workload in workloads)
        {
            var registration = Assert.Single(Services.GetServices<WorkloadRegistration>(),
                candidate => candidate.Name == workload);
            var projector = (Projector)Services.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(TenantId.ToString())).RunAsync(projector);
            var runner = new ProjectorRunner(Events);
            var checkpoint = _checkpoints.GetValueOrDefault(workload, ProjectionCheckpoint.Start);
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }

            _checkpoints[workload] = checkpoint;
        }
    }

    public async Task<List<TEvent>> EventsAsync<TEvent>(EventStreamAddress stream) where TEvent : DomainEvent
    {
        var found = new List<TEvent>();
        try
        {
            await foreach (var record in Events.ReadAsync(stream, 0))
                if (record.Event is TEvent typed)
                    found.Add(typed);
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unwritten stream this way.
        }

        return found;
    }

    /// <summary>A frozen create-new row for an application the batch declares.</summary>
    public static ApplicationImportPlannedRow Create(Uuid applicationId, string record, string name = "Alpha",
        string purpose = "Observed purpose") => new(Uuid.CreateVersion4(), record, "create_new", applicationId, null,
        name, purpose, null);

    /// <summary>A frozen link-existing row for an application at the given content revision.</summary>
    public static ApplicationImportPlannedRow Link(Uuid applicationId, string record, long expectedRevision) =>
        new(Uuid.CreateVersion4(), record, "link_existing", applicationId, expectedRevision, "Linked", "Observed", null);

    /// <summary>A frozen retire row with the deterministic claim and row identities and a matching impact.</summary>
    public ApplicationImportPlannedRow Retire(Uuid batchId, Uuid applicationId, string record, long expectedRevision,
        string key = "manual", string ns = "applications")
    {
        var claim = ApplicationImportEffects.SourceClaimId(ApplicationImportSource.IdFor(TenantId, key, ns), record);
        var impact = new string('c', 64);
        Impacts.ByApplication[applicationId] = new ApplicationChangePreview(TenantId, applicationId, expectedRevision,
            "retire", [], [], [], true)
        {
            ImpactDigest = impact,
        };
        return new ApplicationImportPlannedRow(ApplicationImportEffects.RetirementRowId(batchId, claim), record,
            "retire", applicationId, expectedRevision, string.Empty, string.Empty, null)
        {
            RetirementSourceClaimId = claim,
            RetirementProposalSha256 = new string('b', 64),
            RetirementImpactDigest = impact,
            RetirementReason = "Omitted by the complete source",
        };
    }

    /// <summary>
    ///     Seals a complete frozen plan of the given rows exactly as acceptance does (planner, canonical groups,
    ///     immutable manifests, first response) and appends the plan seal to the batch.
    /// </summary>
    public async Task<ApplicationImportBatch> SealPlanAsync(Uuid batchId, IReadOnlyList<ApplicationImportPlannedRow> rows,
        Uuid? commandId = null)
    {
        var batch = await BatchAsync(batchId);
        var source = (await ApplicationImportSourceView.LoadAsync(Reader, Content, Events, TenantId, batch.SourceKey!,
            batch.SourceNamespace!, ct: CancellationToken.None)).Value!;
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        foreach (var id in rows.Select(row => row.ApplicationId).Distinct())
            targets[id] = await Reader.HydrateApplicationAsync(TenantId, id);
        var groups = ApplicationImportPlanner.PlanGroups(TenantId, rows, targets);
        Assert.True(groups.IsSuccess, groups.Error?.Message);
        var logical = commandId ?? Uuid.CreateVersion4();
        var now = Clock.GetUtcNow();
        var actor = ActorReference.ForMember(ActorMemberId, "Import manager");
        var header = new DurableIntentHeader(logical, batch.Stream, batch.Stream, batch.Stream, actor, "initial",
            (ulong)batch.Revision, new string('a', 64), now);
        var cut = new AuthoritativeReadCut($"stream://{source.Source.Stream.Realm}/{source.Source.Stream.Area}/" +
            source.Source.Stream.Resource, source.Source.CommittedStreamPosition, now);
        var (plan, _, frozenGroups) = await ApplicationImportPlanWriter.FreezeAsync(Content, TenantId, batchId, header,
            batch, source, rows, groups.Value!, ActorMemberId, "Import manager", cut, CancellationToken.None);
        var response = await ApplicationImportContent.AcceptedResponseAsync(Content, TenantId, logical, now,
            CancellationToken.None);
        var frozen = await FirstResponses.FreezeAsync(Content, TenantId.ToString(), response);
        var provenance = new OperationProvenance(logical, logical, null, "AcceptApplicationImport", actor,
            batch.Stream, "initial", (ulong)batch.Revision, header.AuthoredIntentSha256, now, batch.Stream, batch.Stream);
        var writable = await HydrateAsync(NewBatch(batchId, logical));
        var sealedPlan = writable.SealPlan(provenance, plan.Sha256, plan, rows.Count, frozenGroups.Count, frozen);
        Assert.True(sealedPlan.IsSuccess, sealedPlan.Error?.Message);
        await Writer.SaveAsync(writable, new RequestDispatchContext(RequestActor.System));
        return await BatchAsync(batchId);
    }

    /// <summary>Declares an application and revises it until it is at the requested published revision.</summary>
    public async Task<Uuid> SeedApplicationAsync(int revision = 1, Uuid? applicationId = null)
    {
        var id = applicationId ?? Uuid.CreateVersion4();
        var application = new DeclaredApplication(TenantId, id);
        Assert.True(application.Declare("Seeded", "Existing purpose", null, ActorMemberId, "Import manager",
            StartedAt).IsSuccess);
        await Writer.SaveAsync(application, new RequestDispatchContext(RequestActor.System));
        for (var next = 2; next <= revision; next++)
        {
            var revised = await ApplicationAsync(id);
            Assert.Null(revised.Revise(next - 1, "Seeded", $"Revised purpose {next}", null, ActorMemberId,
                "Import manager", StartedAt));
            await Writer.SaveAsync(revised, new RequestDispatchContext(RequestActor.System));
        }

        return id;
    }

    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    public sealed class ManualClock(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _now = utcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    public sealed class SwitchableTenantActivity : ITenantActivity
    {
        public bool Active { get; set; } = true;

        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(Active);
    }

    /// <summary>The retirement impact the trusted worker rereads; unset applications are unavailable.</summary>
    public sealed class FixedRetirementImpacts : IApplicationImportRetirementImpactReader, IApplicationChangeImpactReader
    {
        public Dictionary<Uuid, ApplicationChangePreview> ByApplication { get; } = [];

        public ValueTask<Result<ApplicationChangePreview>> ReadAsync(PreviewApplicationChange request, Uuid userId,
            CancellationToken ct) => ReadForImportWorkerAsync(request, ct);

        public ValueTask<Result<ApplicationChangePreview>> ReadForImportWorkerAsync(PreviewApplicationChange request,
            CancellationToken ct) => ValueTask.FromResult(ByApplication.TryGetValue(request.ApplicationId, out var impact)
            ? Result<ApplicationChangePreview>.Success(impact)
            : Result<ApplicationChangePreview>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Impact unavailable.", isTransient: true)));
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
