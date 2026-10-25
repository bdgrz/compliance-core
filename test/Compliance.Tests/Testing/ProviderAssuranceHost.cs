using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes the provider assurance owners end to end over in-memory Fitz and an in-memory event store: the real
///     request bus with every handler, authorizer and process definition of the compliance composition, a test
///     source-fact resolver and a catch-up of the assurance directory. A test sends the public commands as a
///     member, then drives the review or link process exactly as the reactors would, or leaves it pending to
///     exercise a paused receiver.
/// </summary>
sealed class ProviderAssuranceHost : IAsyncDisposable
{
    readonly ServiceProvider _provider;

    ProviderAssuranceHost(IImmutableContentStore? content = null, TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        Events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        if (content is not null)
            services.AddSingleton(content);
        if (clock is not null)
            services.AddSingleton(clock);
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        services.AddScoped<IProgramResourceScopeResolver>(_ => new TestProgramResourceScopeResolver());
        services.AddScoped<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public Uuid Tenant { get; } = Uuid.CreateVersion4();

    public Uuid User { get; } = Uuid.CreateVersion4();

    public Uuid ProviderId { get; private set; }

    public HostPermissions Permissions { get; } = new();

    public InMemoryEventStore Events { get; }

    public IServiceProvider Services => _provider;

    public IImmutableContentStore Content => _provider.GetRequiredService<IImmutableContentStore>();

    /// <summary>
    ///     Creates a host whose tenant holds one material provider that declares <see cref="ServiceId" /> for
    ///     <see cref="ProgramId" />, seeded directly in the provider register.
    /// </summary>
    public static async Task<ProviderAssuranceHost> CreateAsync(IImmutableContentStore? immutableContent = null, TimeProvider? clock = null)
    {
        var host = new ProviderAssuranceHost(immutableContent, clock) { ProviderId = Uuid.CreateVersion4() };
        var content = MaterialProvider() with
        {
            Dependencies =
            [
                new ProviderDependency("client_service", ServiceId, ProgramId, null, "Payroll",
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            ],
        };
        await NamedRecordFacts.SeedProviderAsync(host.Services, host.Tenant, host.ProviderId, content, Author, Now);
        return host;
    }

    // --- public commands

    /// <summary>Dispatches a request through the real bus as a member over the named transport.</summary>
    public async Task<Result<T>> SendAsync<T>(IRequest<T> request, string transport = "http",
        Uuid? user = null, RequestMetadata? metadata = null)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user ?? User), InvocationOf(transport), metadata),
            CancellationToken.None);
    }

    static RequestInvocation InvocationOf(string transport) => transport switch
    {
        "http" => new HttpInvocation("PUT", "/synthetic/provider/assurance", "/synthetic/provider/assurance", "synthetic"),
        "mcp" => new McpInvocation("synthetic.provider.assurance"),
        _ => new DirectInvocation(),
    };

    public async Task<AssuranceReportRegistration> RecordReportAsync(Uuid? reportId = null,
        AssuranceReportContent? content = null)
    {
        var id = reportId ?? Uuid.CreateVersion4();
        var result = await SendAsync(new RecordAssuranceReport(id, Tenant, ProviderId, content ?? Report()));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    public async Task<AssuranceReportRegistration> ReviseReportAsync(Uuid reportId, long expected,
        AssuranceReportContent? content = null, Uuid? commandId = null)
    {
        var result = await SendAsync(new ReviseAssuranceReport(commandId ?? Uuid.CreateVersion4(), Tenant, ProviderId,
            reportId, expected, content ?? Report() with { Opinion = "qualified" }));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    public async Task<ProviderCoverageGapRegistration> RecordGapAsync(Uuid? gapId = null,
        ProviderCoverageGapContent? content = null)
    {
        var id = gapId ?? Uuid.CreateVersion4();
        var gap = content ?? await GapContentAsync();
        await CatchUpAsync();
        var result = await SendAsync(new RecordProviderCoverageGap(id, Tenant, ProviderId, gap));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    /// <summary>A valid gap against a recorded report of this provider, which declares the service the gap names.</summary>
    public async Task<ProviderCoverageGapContent> GapContentAsync()
    {
        var report = await RecordReportAsync();
        return new ProviderCoverageGapContent(ServiceId, "Payroll processing", "Availability assertion",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "assurance_report", report.ReportId, report.Revision,
            "The SOC report does not cover the service for the full period.");
    }

    /// <summary>Closes a gap against a newer or different source after the directory caught up.</summary>
    public async Task<Result<ProviderCoverageGapRegistration>> CloseGapAsync(Uuid gapId, long expected,
        ProviderCoverageGapClosureContent content, Uuid? commandId = null)
    {
        await CatchUpAsync();
        return await SendAsync(new CloseProviderCoverageGap(commandId ?? Uuid.CreateVersion4(), Tenant, ProviderId, gapId,
            expected, content));
    }

    /// <summary>Requests a link of an accepted risk to a gap.</summary>
    public async Task<Result<OperationAcceptedResponse>> LinkRequestAsync(Uuid gapId, long expected, Uuid riskId,
        Uuid acceptanceId, Uuid? commandId = null, string transport = "http")
    {
        await AssuranceProjectionCatchUp.RunProvidersAsync(_provider, Tenant);
        return await SendAsync(new LinkProviderCoverageGapRiskAcceptance(commandId ?? Uuid.CreateVersion4(), Tenant,
            ProgramId, ProviderId, gapId, expected, riskId, acceptanceId), transport);
    }

    public static readonly Uuid ProgramId = Uuid.CreateVersion4();

    public static readonly Uuid ServiceId = Uuid.CreateVersion4();

    // --- sources

    /// <summary>Seeds an active, personally accepted risk of <see cref="ProgramId" /> and returns its identities.</summary>
    async Task SeedAsync<TOut>(RiskDraftRules source, Func<RiskDraftRules, Result<TOut>> operation)
    {
        var result = operation(source);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await using var scope = _provider.CreateAsyncScope();
        await RiskDraftSeeds.WriteAsync(scope.ServiceProvider.GetRequiredService<IAggregateWriter>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), source);
    }

    public async Task<(Uuid RiskId, Uuid AcceptanceId, DateTimeOffset ExpiresAt)> AcceptRiskAsync(
        TimeSpan? lifetime = null)
    {
        var identifier = "R-" + Uuid.CreateVersion4().ToString()[..8];
        var riskId = RiskDraft.IdFor(Tenant, ProgramId, identifier);
        var approver = Uuid.CreateVersion4();
        var assessor = Uuid.CreateVersion4();
        var acceptanceId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(lifetime ?? TimeSpan.FromDays(90));
        await SeedAsync(new RiskDraftRules(Tenant, riskId), draft => draft.Create(ProgramId, Uuid.CreateVersion4(),
            identifier, new RiskDraftContent("Provider exposure", "Provider coverage is incomplete.",
                "Customer impact", null), assessor, "Assessor", now.AddMinutes(-5)));
        var method = new RiskMethodRules(Tenant, ProgramId);
        Assert.Null(method.Publish(ProgramId, 0, ["1", "2", "3", "4", "5"], ["1", "2", "3", "4", "5"], 12,
            ActorReference.ForMember(approver, "Lead"), now.AddMinutes(-5)));
        var version = method.Current!;
        var residualId = Uuid.CreateVersion4();
        await SeedAsync(new RiskEvaluationRules(Tenant, riskId), evaluation =>
        {
            var failure = evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(), version, "inherent", 5, 5,
                "Severe", assessor, "Assessor", now.AddMinutes(-4)) ??
                evaluation.ChooseTreatment(ProgramId, 1, "accept", "Cost exceeds benefit", assessor, "Assessor",
                    now.AddMinutes(-3)) ??
                evaluation.RecordAssessment(ProgramId, 2, residualId, version, "residual", 3, 4, "As accepted",
                    assessor, "Assessor", now.AddMinutes(-2)) ??
                evaluation.Accept(ProgramId, 3, acceptanceId, residualId, version, "compliance_lead", true, expires,
                    "Within appetite", approver, "Lead", now.AddMinutes(-1));
            return failure is null ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Validation,
                failure.Message ?? string.Empty));
        });
        return (riskId, acceptanceId, expires);
    }

    public async Task SeedAsync(RiskEvaluationRules selected, Func<RiskEvaluationRules, Result> operation)
    {
        await using var scope = _provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await new RiskEvaluationReader(reader, Events, Content).RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await RiskEvaluationSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), Events, Content, rules);
    }

    public async Task SeedAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, Result> operation)
        where TAggregate : Aggregate
    {
        await using var scope = _provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
            item => AggregateOutcome.Commit(operation(item)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    public async Task SeedAsync<TAggregate, TOut>(TAggregate aggregate, Func<TAggregate, Result<TOut>> operation)
        where TAggregate : Aggregate =>
        await SeedAsync(aggregate, item =>
        {
            var result = operation(item);
            return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
        });

    public async Task<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate) where TAggregate : Aggregate
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(aggregate);
    }

    public Task<ProviderAssuranceReport> ReportAsync(Uuid reportId, Uuid? selected = null) =>
        HydrateAsync(new ProviderAssuranceReport(Tenant, ProviderId, reportId, selected ?? Uuid.Empty));

    public Task<ProviderAssuranceReview> ReviewAsync(Uuid reviewId, Uuid? selected = null) =>
        HydrateAsync(new ProviderAssuranceReview(Tenant, reviewId, selected ?? Uuid.Empty));

    public Task<ProviderAssuranceGap> GapAsync(Uuid gapId, Uuid? selected = null) =>
        HydrateAsync(new ProviderAssuranceGap(Tenant, ProviderId, gapId, selected ?? Uuid.Empty));

    public Task<ProviderRiskAcceptance> LinkAsync(Uuid linkId, Uuid? selected = null) =>
        HydrateAsync(new ProviderRiskAcceptance(Tenant, linkId, selected ?? Uuid.Empty));

    public async Task<List<DomainEvent>> FactsAsync(EventStreamAddress stream)
    {
        var facts = new List<DomainEvent>();
        try
        {
            await foreach (var record in Events.ReadAsync(stream, 0))
                facts.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return facts;
    }

    // --- the processes

    public Task<ProcessKey> ReviewKey(Uuid reviewId) => ProcessKeys.FindAsync(Services, "ProviderReviewProcess", Tenant.ToString(), reviewId);

    public Task<ProcessKey> LinkKey(Uuid linkId) => ProcessKeys.FindAsync(Services, "IndependentChildMaterialization", Tenant.ToString(), linkId);

    /// <summary>The root admission fact of a review or link, as its reactor would see it.</summary>
    public async Task<DomainEventRecord> RootAsync(EventStreamAddress stream, Uuid logicalId)
    {
        await foreach (var record in Events.ReadAsync(stream, 0))
        {
            if (record.Event is ProviderReviewRequested or ProviderCoverageGapRiskAcceptanceAdmitted &&
                ((INativeOperationFact)record.Event).Operation.LogicalId == logicalId)
                return record;
        }

        throw new InvalidOperationException($"No admission fact of {logicalId} exists on {stream}.");
    }

    /// <summary>Starts and drives the review process of one admitted review, exactly as its reactor would.</summary>
    public async Task<ProcessRecord> DriveReviewAsync(Uuid reviewId)
    {
        var record = await RootAsync(ProviderAssuranceStreams.Review(Tenant, reviewId), reviewId);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessRootReaction>().StartAsync(
            "ProviderReviewProcess", new RecordReactorContext(record), ((ProviderReviewRequested)record.Event).Intent,
            [], "test");
    }

    /// <summary>Starts and drives the child materialization of one admitted link, exactly as its reactor would.</summary>
    public async Task<ProcessRecord> DriveLinkAsync(Uuid linkId, Uuid gapId)
    {
        var record = await RootAsync(ProviderAssuranceStreams.Gap(Tenant, ProviderId, gapId), linkId);
        var admitted = (ProviderCoverageGapRiskAcceptanceAdmitted)record.Event;
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessRootReaction>().StartAsync(
            IndependentChildMaterializationProcess.Name, new RecordReactorContext(record), admitted.Intent, [], "test");
    }

    /// <summary>Creates the process record without driving it, as the reactor does before its first dispatch.</summary>
    public async Task<ProcessRecord> PrepareProcessAsync(ProcessKey key, DomainEventRecord root,
        ImmutableContentRef intent)
    {
        await using var scope = _provider.CreateAsyncScope();
        var native = (INativeOperationFact)root.Event;
        return await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(key, NativeFactReference.Of(root.Stream, root.Event), native.Operation.Episode, intent,
                [], DateTimeOffset.UtcNow));
    }

    /// <summary>Resumes an existing process record, as the sweep would.</summary>
    public async Task<ProcessRecord> ResumeAsync(ProcessKey key, string worker = "sweep")
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(key, worker,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    public async Task<ProcessRecord?> ProcessAsync(ProcessKey key)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().ReadAsync(key);
    }

    /// <summary>Builds the exact receiver command the driver would send for a step of the record.</summary>
    public async Task<IRequest<ReceiverOutcome>> CommandAsync(ProcessRecord record, string receiver, string stage)
    {
        await using var scope = _provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ProcessCatalog>();
        var step = new ProcessStep(record, stage, receiver, null, Bdgrz.Compliance.Features.Kernel.EffectIdentity.CommandId(
            record.Source.EventId, $"{record.Key.ProcessKind}:{receiver}", "-", record.Episode));
        return await catalog.Get(record.Key.ProcessKind).CreateReceiver(step, scope.ServiceProvider,
            CancellationToken.None) ?? throw new InvalidOperationException($"{receiver} is not selected.");
    }

    /// <summary>Sends one receiver command through the real bus as the system principal.</summary>
    public async Task<Result<ReceiverOutcome>> ReceiveAsync(IRequest<ReceiverOutcome> command,
        System.Security.Claims.ClaimsPrincipal? actor = null)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command,
            new RequestDispatchContext(actor ?? RequestActor.System), CancellationToken.None);
    }

    // --- the projection

    /// <summary>Applies every assurance fact committed so far to the directory, as the projector would.</summary>
    public async Task CatchUpAsync()
    {
        await AssuranceProjectionCatchUp.RunProvidersAsync(_provider, Tenant);
        await AssuranceProjectionCatchUp.RunAsync(_provider, Tenant);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

}

/// <summary>A switchable permission grant: every permission unless a specific set is granted.</summary>
sealed class HostPermissions : IPermissionAuthorizer
{
    public bool Allowed { get; set; } = true;

    public string[]? Granted { get; set; }

    public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
        CancellationToken ct = default) => ValueTask.FromResult(
        Allowed && (Granted is null || Granted.Contains(permission, StringComparer.Ordinal)));
}

/// <summary>Runs the assurance projector over the committed facts of one tenant, as the worker would.</summary>
static class AssuranceProjectionCatchUp
{
    /// <summary>Applies the committed provider revisions of one tenant to the provider directory.</summary>
    public static async Task RunProvidersAsync(IServiceProvider provider, Uuid tenant)
    {
        await using var scope = provider.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<FitzProviderDirectory>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await directory.LoadCheckpointAsync(tenant);
        var pattern = EventStreamPattern.ForPattern(tenant.ToString(), Provider.Area);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzProviderDirectory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, CancellationToken.None))
        {
            var reference = record.Event switch
            {
                ProviderRecorded recorded => recorded.ContentContent,
                ProviderRevised revised => revised.ContentContent,
                _ => null,
            };
            if (reference is not null)
            {
                var contentStore = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
                var bytes = await contentStore.ReadVerifiedAsync(tenant.ToString(), reference, CancellationToken.None);
                var content = System.Text.Json.JsonSerializer.Deserialize(bytes.Span,
                    ComplianceCoreJsonContext.Default.ProviderContent)
                    ?? throw new InvalidOperationException("The provider content is empty.");
                await directory.ApplyAsync(record.Event, content);
            }
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    public static async Task RunAsync(IServiceProvider provider, Uuid tenant)
    {
        await using var scope = provider.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<FitzAssuranceDirectory>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var projector = new AssuranceProjector(directory, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        var checkpoint = await directory.LoadCheckpointAsync(tenant);
        var pattern = EventStreamPattern.ForPattern(tenant.ToString(), ProviderAssuranceStreams.Area);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzAssuranceDirectory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, CancellationToken.None))
        {
            var task = record.Event switch
            {
                AssuranceReportRecorded ev => projector.HandleAsync(ev, null!, default),
                AssuranceReportRevised ev => projector.HandleAsync(ev, null!, default),
                ProviderReviewRecorded ev => projector.HandleAsync(ev, null!, default),
                ProviderCoverageGapRecorded ev => projector.HandleAsync(ev, null!, default),
                ProviderCoverageGapClosed ev => projector.HandleAsync(ev, null!, default),
                ProviderCoverageGapRiskAcceptanceAdmitted ev => projector.HandleAsync(ev, null!, default),
                ProviderCoverageGapRiskAcceptanceLinked ev => projector.HandleAsync(ev, null!, default),
                _ => ValueTask.CompletedTask,
            };
            await task;
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
