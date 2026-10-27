using System.Security.Claims;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The full composition over in-memory stores for the advisor handoff and feedback thread owners. Handlers are
///     built from the same scope as the production ones, committed admissions are turned into process records the
///     way the reactor does, and the clock and the source resolver are scripted.
/// </summary>
sealed class AdvisorHost : IAsyncDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 10, 17, 12, 2, 0, TimeSpan.Zero);
    public static readonly DateOnly Expiry = new(2026, 12, 31);

    readonly ServiceProvider _owned;
    readonly Func<IAggregateExecutor, IAggregateExecutor>? _wrap;

    public AdvisorHost(DateTimeOffset? now = null, Func<IAggregateExecutor, IAggregateExecutor>? wrapExecutor = null,
        Func<IProcessRecordStore, IProcessRecordStore>? store = null)
    {
        _wrap = wrapExecutor;
        Clock = new HostClock(now ?? Start);
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
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IPlatformUserDirectoryReader>(new ExistingUsers());
        services.AddSingleton<TimeProvider>(Clock);
        if (store is not null)
        {
            services.RemoveAll<IProcessRecordStore>();
            services.AddSingleton<IProcessRecordStore>(provider =>
                store(new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>())));
        }

        services.RemoveAll<IAdvisorRecordVersionResolver>();
        services.AddSingleton<IAdvisorRecordVersionResolver>(Resolver);
        _owned = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Scope = _owned.CreateAsyncScope();
    }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public Uuid ProgramId { get; } = Uuid.CreateVersion4();

    public Uuid UserId { get; } = Uuid.CreateVersion4();

    public HostClock Clock { get; }

    public ScriptedResolver Resolver { get; } = new();

    public AsyncServiceScope Scope { get; }

    public IServiceProvider Provider => _owned;

    public IServiceProvider Services => Scope.ServiceProvider;

    public IAggregateReader Reader => Services.GetRequiredService<IAggregateReader>();

    public IAggregateWriter Writer => Services.GetRequiredService<IAggregateWriter>();

    public IImmutableContentStore Content => Services.GetRequiredService<IImmutableContentStore>();

    public IDomainEventReader Events => Services.GetRequiredService<IDomainEventReader>();

    public AdvisorFeedbackReader Threads => Services.GetRequiredService<AdvisorFeedbackReader>();

    public AdvisorHandoffReader Handoffs => Services.GetRequiredService<AdvisorHandoffReader>();

    public ActorReference Lead => ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Compliance lead");

    IAggregateExecutor? _executor;

    NativeOperationExecutor Operations
    {
        get
        {
            var executor = Services.GetRequiredService<IAggregateExecutor>();
            _executor ??= _wrap?.Invoke(executor) ?? executor;
            return new NativeOperationExecutor(Reader, _executor, Content);
        }
    }

    AdvisorUnitOperation Unit => new(Operations, Content);

    public CreateAdvisorHandoffHandler CreateHandler => new(Unit, Handoffs, Resolver, Content, Clock);

    public ReviseAdvisorHandoffSelectionHandler ReviseHandler => new(Unit, Handoffs, Resolver, Content, Clock);

    public MarkAdvisorHandoffReadyForReviewHandler ReadyHandler => new(Unit, Handoffs, Resolver, Clock);

    public CloseAdvisorHandoffHandler CloseHandler => new(Unit, Content, Clock);

    public RevokeAdvisorHandoffHandler RevokeHandler => new(Unit, Content, Clock);

    public RecordAdvisorFeedbackHandler RecordHandler => new(Operations, Handoffs, Content, Clock);

    public CorrectAdvisorFeedbackHandler CorrectHandler => new(Unit, Threads, Content, Clock);

    public RespondToAdvisorFeedbackHandler RespondHandler => new(Unit, Threads, Content, Clock);

    public ResolveAdvisorFeedbackHandler ResolveHandler => new(Unit, Threads, Clock);

    public MaterializeAdvisorFeedbackHandler MaterializeHandler => new(Reader,
        Services.GetRequiredService<IAggregateExecutor>(), Handoffs, Content, Clock);

    public ListAdvisorFeedbackHandler ListFeedbackHandler => new(Threads, Handoffs, Resolver);

    public IProcessRecordStore Records => Services.GetRequiredService<IProcessRecordStore>();

    /// <summary>A personal HTTP principal of the compliance lead.</summary>
    public Cntryl.Portia.RequestContext<T> Http<T>(T request, Uuid? userId = null) where T : IRequestBase => new(request,
        new RequestDispatchContext(Personal(userId ?? UserId), new HttpInvocation("PUT",
            "/api/v1/advisor-handoffs/test", "/api/v1/advisor-handoffs/test", "advisor-host")));

    /// <summary>The same request dispatched by the system actor, which no advisor command accepts.</summary>
    public static Cntryl.Portia.RequestContext<T> System<T>(T request) where T : IRequestBase => new(request, RequestActor.System);

    public static ClaimsPrincipal Personal(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("email", "lead@example.test")],
        "BdgrzSession"));

    public static AdvisorRecordVersion NewSource() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4());

    public async Task<Result> CreateAsync(Uuid handoffId, IReadOnlyList<AdvisorRecordVersion> selection,
        string purpose = "Type I controls", Uuid? comparedTo = null) => await CreateHandler.HandleAsync(
        Http(new CreateAdvisorHandoff(TenantId, ProgramId, handoffId, selection, purpose, Expiry, comparedTo)),
        CancellationToken.None);

    public async Task<Result> ReadyAsync(Uuid handoffId, long expectedRevision, Uuid? commandId = null) =>
        await ReadyHandler.HandleAsync(Http(new MarkAdvisorHandoffReadyForReview(commandId ?? Uuid.CreateVersion4(),
            TenantId, ProgramId, handoffId, expectedRevision)), CancellationToken.None);

    public async Task<Result> ReviseAsync(Uuid handoffId, long expectedRevision,
        IReadOnlyList<AdvisorRecordVersion> selection, string reason = "Changed scope", Uuid? commandId = null) =>
        await ReviseHandler.HandleAsync(Http(new ReviseAdvisorHandoffSelection(commandId ?? Uuid.CreateVersion4(),
            TenantId, ProgramId, handoffId, expectedRevision, selection, reason)), CancellationToken.None);

    public async Task<Result> EndAsync(Uuid handoffId, long expectedRevision, bool revoke, Uuid? commandId = null)
    {
        var id = commandId ?? Uuid.CreateVersion4();
        return revoke
            ? await RevokeHandler.HandleAsync(Http(new RevokeAdvisorHandoff(id, TenantId, ProgramId, handoffId,
                "The review is no longer required", expectedRevision)), CancellationToken.None)
            : await CloseHandler.HandleAsync(Http(new CloseAdvisorHandoff(id, TenantId, ProgramId, handoffId,
                "The review is complete", expectedRevision)), CancellationToken.None);
    }

    public RecordAdvisorFeedback Feedback(Uuid handoffId, Uuid feedbackId, AdvisorRecordVersion source,
        long expectedRevision, string body = "Please clarify the control owner.", string kind = "comment") =>
        new(TenantId, ProgramId, handoffId, feedbackId, source.RecordId, source.VersionId, kind, body,
            expectedRevision)
        {
            SelectedSource = source.IsSupported ? source : null,
        };

    public async Task<Result<OperationAcceptedResponse>> RecordAsync(RecordAdvisorFeedback request,
        Uuid? advisorUserId = null) => await RecordHandler.HandleAsync(Http(request, advisorUserId),
        CancellationToken.None);

    /// <summary>Creates a handoff and opens it for review: it is at revision two.</summary>
    public async Task<(Uuid HandoffId, AdvisorRecordVersion Source)> SeedReadyAsync(
        AdvisorRecordVersion? source = null)
    {
        var handoffId = Uuid.CreateVersion4();
        var selected = source ?? NewSource();
        Assert.True((await CreateAsync(handoffId, [selected])).IsSuccess);
        Assert.True((await ReadyAsync(handoffId, 1)).IsSuccess);
        return (handoffId, selected);
    }

    /// <summary>Admits feedback on a ready handoff at revision two and materializes its thread.</summary>
    public async Task<(Uuid HandoffId, Uuid FeedbackId, AdvisorRecordVersion Source)> SeedFeedbackAsync(
        Uuid? advisorUserId = null)
    {
        var (handoffId, source) = await SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        var recorded = await RecordAsync(Feedback(handoffId, feedbackId, source, 2), advisorUserId);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        await MaterializeAsync(handoffId);
        return (handoffId, feedbackId, source);
    }

    public Task<ProcessKey> KeyAsync(Uuid rootOperationId) => ProcessKeys.FindAsync(Provider,
        FindingClosureEffects.MaterializationProcessName, TenantId.ToString(), rootOperationId);

    /// <summary>Starts and drives the materialization process of every feedback the handoff admitted.</summary>
    public async Task<IReadOnlyList<ProcessRecord>> MaterializeAsync(Uuid handoffId)
    {
        var handoff = await Handoffs.ReadAsync(TenantId, ProgramId, handoffId);
        var driven = new List<ProcessRecord>();
        foreach (var admitted in handoff.Admitted)
        {
            var key = ProcessKey.ForRoot(FindingClosureEffects.MaterializationProcessName,
                admitted.Operation.RootOperationId, NativeFactReference.Of(handoff.Stream, admitted));
            await Records.CreateIfAbsentAsync(ProcessRecord.Start(key, NativeFactReference.Of(handoff.Stream, admitted),
                admitted.Operation.Episode, admitted.Intent, [admitted.Operation.LogicalId.ToString()],
                DateTimeOffset.UtcNow));
            await using var scope = Provider.CreateAsyncScope();
            driven.Add(await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(key, "test-worker",
                new RequestDispatchContext(RequestActor.System), CancellationToken.None));
        }

        return driven;
    }

    public async Task<AdvisorHandoffSnapshot> HandoffAsync(Uuid handoffId) =>
        await Handoffs.ReadAsync(TenantId, ProgramId, handoffId);

    public async Task<AdvisorHandoffSnapshot> SnapshotAsync(Uuid handoffId) =>
        await Handoffs.ReadAsync(TenantId, ProgramId, handoffId);

    public async Task<AdvisorFeedbackThreadSnapshot> ThreadAsync(Uuid handoffId, Uuid feedbackId) =>
        await Threads.ReadAsync(TenantId, ProgramId, handoffId, feedbackId);

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

    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        await _owned.DisposeAsync();
    }

    public sealed class HostClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    /// <summary>Accepts every selection until told to reject, and records what was checked.</summary>
    public sealed class ScriptedResolver : IAdvisorRecordVersionResolver
    {
        public RequestErrorKind? Reject { get; set; }

        public List<IReadOnlyList<AdvisorRecordVersion>> Checked { get; } = [];

        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default)
        {
            Checked.Add(selection);
            return ValueTask.FromResult(Reject is { } kind
                ? Result.Failure(new RequestError(kind, "A selected source version is no longer current."))
                : Result.Success);
        }
    }

    /// <summary>Commits the first attempt but reports it as unknown, as a timeout after append would.</summary>
    public sealed class CommitThenFailOnceExecutor(IAggregateExecutor inner, int failOnCall = 1) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
            if (Interlocked.Increment(ref _calls) == failOnCall)
                throw new EventStreamConcurrencyException("Append outcome unknown.", new TimeoutException());
            return result;
        }
    }

    /// <summary>
    ///     Runs another command on the same owner after the chosen native append was prepared and before it is
    ///     appended, as a concurrent request that wins the stream would.
    /// </summary>
    public sealed class InterleaveExecutor(IAggregateExecutor inner, int onCall, Func<Task> interfere)
        : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == onCall)
                await interfere();
            return await inner.ExecuteAsync(aggregate, operation, context, ct);
        }
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
