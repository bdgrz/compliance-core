using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

/// <summary>Runs request admission, independent child materialization and resolution over the real in-memory kernel.</summary>
sealed class AuditorRequestFixture : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;

    public AuditorRequestFixture(bool allowArtifactRead = true)
    {
        var services = new ServiceCollection();
        Events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddSingleton<TimeProvider>(new FixedClock());
        var kv = new PersistenceKvClient(new InMemoryKvClient());
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(kv));
        services.AddSingleton<IProcessRecordStore>(new FitzKvProcessRecordStore(kv));
        services.AddSingleton(IndependentChildMaterializationProcess.Definition());
        services.AddSingleton(AuditorRequestResolutionProcess.Definition());
        services.AddSingleton<ProcessCatalog>(provider => new(provider.GetServices<ProcessDefinition>()));
        services.AddSingleton<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        services.AddScoped<NativeOperationExecutor>();
        services.AddScoped<AuditorRequestReader>();
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        services.AddScoped<AuditorRequestResolutionVerifier>();
        services.AddScoped<AuditorRequestResolutionFences>();
        services.AddScoped<SampleSelectionAdmission>();
        services.AddSingleton<IChildMaterializationReceiver, AuditorResponseMaterializationReceiver>();
        services.AddSingleton<IChildMaterializationReceiver, AuditorCommentMaterializationReceiver>();
        services.AddSingleton<IChildMaterializationReceiver, AuditorSampleSelectionMaterializationReceiver>();
        services.AddScoped<AuditorResponseEvidence>(provider => new(
            new EvidenceArtifactDownloadAccess(new ArtifactPermission(allowArtifactRead)),
            new EvidenceArtifactMetadataRead(provider.GetRequiredService<IAggregateReader>(), Events),
            provider.GetRequiredService<TimeProvider>()));
        services.AddPortia()
            .AddRequestHandler<MaterializeAuditorResponseHandler>()
            .AddRequestHandler<MaterializeAuditorCommentHandler>()
            .AddRequestHandler<MaterializeAuditorSampleSelectionHandler>()
            .AddRequestHandler<AuthorizeAuditorResponseRequestResolutionUseHandler>()
            .AddRequestHandler<CompleteAuditorRequestResolutionHandler>()
            .AddRequestHandler<SettleAuditorResponseRequestResolutionUseHandler>()
            .AddRequestHandler<AbortAuditorRequestResolutionHandler>()
            .AddRequestAuthorizer<AuditorRequestReceiverAuthorizer>();
        services.AddVerifiedSourceUseReader();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = Services.GetRequiredService<IAggregateReader>();
        Writer = Services.GetRequiredService<IAggregateWriter>();
        Content = Services.GetRequiredService<IImmutableContentStore>();
    }

    public IServiceProvider Services => _scope.ServiceProvider;
    public InMemoryEventStore Events { get; }
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IImmutableContentStore Content { get; }
    public Uuid TenantId { get; } = Uuid.CreateVersion4();
    public Uuid ProgramId { get; } = Uuid.CreateVersion4();
    public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
    public Uuid UserId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerId { get; } = Uuid.CreateVersion4();
    public Uuid MemberId => RbacIds.Member(TenantId, UserId);

    public T Handler<T>() where T : notnull => ActivatorUtilities.CreateInstance<T>(Services);

    public Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid? userId = null) where T : IRequestBase => new(request,
        new RequestDispatchContext(Actor(userId ?? UserId),
            new HttpInvocation("POST", "/api/v1/auditor-requests", "/api/v1/auditor-requests", "auditor-test")));

    public static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "attest@example.test")], "BdgrzSession"));

    public RecordAuditorRequest NewRecordCommand() => new(TenantId, ProgramId, Uuid.CreateVersion4(),
        ExaminationId, "external_email", "mail-001", "External request wording",
        "Evidence for the examination period", "normal", MemberId, new DateOnly(2026, 10, 31));

    public async Task SeedMemberAsync(Uuid userId)
    {
        var member = new Member(TenantId, userId);
        Assert.True(member.Register().IsSuccess);
        await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
    }

    public async Task<Uuid> SeedRequestAsync()
    {
        await SeedMemberAsync(UserId);
        var command = NewRecordCommand();
        var recorded = await Handler<RecordAuditorRequestHandler>().HandleAsync(Context(command), CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        return command.RequestId;
    }

    public ValueTask<AuditorRequest> RequestAsync(Uuid id) => Reader.HydrateAsync(new AuditorRequest(TenantId, ProgramId, id));
    public async Task<AuditorRequestHistorySnapshot> RequestHistoryAsync(Uuid id) =>
        await AuditorRequestHistory.ReadAsync(Events, await RequestAsync(id));
    public AuditorRequestReader Requests => Services.GetRequiredService<AuditorRequestReader>();

    public ValueTask<AuditorResponse> ResponseAsync(Uuid requestId, Uuid id) =>
        Reader.HydrateAsync(new AuditorResponse(TenantId, ProgramId, requestId, id));

    public async Task<IReadOnlyList<DomainEvent>> ResponseHistoryAsync(Uuid requestId, Uuid id) =>
        await AuditorResponseHistory.ReadAsync(Events, await ResponseAsync(requestId, id));

    public async Task<AuditorRequestTermsContent> TermsAsync(Uuid requestId) =>
        await ReadAsync<AuditorRequestTermsContent>((await RequestAsync(requestId)).Terms!);

    public Task<T> ReadAsync<T>(ImmutableContentRef reference) where T : notnull =>
        Content.ReadJsonAsync<T>(TenantId.ToString(), reference);

    public async Task<Result<OperationAcceptedResponse>> DraftAsync(Uuid requestId, Uuid responseId,
        string summary = "Complete response", IReadOnlyList<Uuid>? evidence = null, long? expected = null)
    {
        var request = new CreateAuditResponseDraft(TenantId, ProgramId, requestId, responseId, summary,
            evidence ?? [], expected ?? (await RequestAsync(requestId)).Revision);
        return await Handler<CreateAuditResponseDraftHandler>().HandleAsync(Context(request), CancellationToken.None);
    }

    public async Task<Result> SubmitAsync(Uuid requestId, Uuid responseId)
    {
        var owner = await ResponseAsync(requestId, responseId);
        return await Handler<SubmitAuditResponseHandler>().HandleAsync(Context(new SubmitAuditResponse(
            Uuid.CreateVersion4(), TenantId, ProgramId, requestId, responseId, owner.BusinessVersion,
            "Submitted for independent review", owner.Revision)), CancellationToken.None);
    }

    public async Task<Result> ReviewAsync(Uuid requestId, Uuid responseId, string outcome = "approved", Uuid? actor = null)
    {
        var owner = await ResponseAsync(requestId, responseId);
        return await Handler<ReviewAuditResponseHandler>().HandleAsync(Context(new ReviewAuditResponse(
            Uuid.CreateVersion4(), TenantId, ProgramId, requestId, responseId, owner.BusinessVersion,
            outcome, "Reviewed exact response", owner.Revision), actor ?? ReviewerId), CancellationToken.None);
    }

    public async Task<Result> DeliverAsync(Uuid requestId, Uuid responseId)
    {
        var owner = await ResponseAsync(requestId, responseId);
        return await Handler<DeliverAuditResponseHandler>().HandleAsync(Context(new DeliverAuditResponse(
            Uuid.CreateVersion4(), TenantId, ProgramId, requestId, responseId, owner.BusinessVersion,
            "External auditor", "delivery-001", owner.Revision)), CancellationToken.None);
    }

    public async Task<Result> AcceptAsync(Uuid requestId, Uuid responseId)
    {
        var owner = await ResponseAsync(requestId, responseId);
        return await Handler<RecordAuditorResponseDispositionHandler>().HandleAsync(Context(
            new RecordAuditorResponseDisposition(Uuid.CreateVersion4(), TenantId, ProgramId, requestId, responseId,
                owner.BusinessVersion, "accepted", "receipt-001", owner.Revision)), CancellationToken.None);
    }

    public async Task<ProcessRecord> MaterializeAsync(Uuid operationId)
    {
        var record = await RootAsync(operationId, nameof(AuditorResponseAdmitted), nameof(AuditorCommentAdmitted),
            nameof(AuditorSampleSelectionAdmitted));
        var intent = record.Event switch
        {
            AuditorResponseAdmitted fact => fact.Intent,
            AuditorCommentAdmitted fact => fact.Intent,
            AuditorSampleSelectionAdmitted fact => fact.Intent,
            _ => throw new InvalidOperationException("The root is not a child admission."),
        };
        return await Services.GetRequiredService<ProcessRootReaction>().StartAsync(
            IndependentChildMaterializationProcess.Name, new RecordReactorContext(record), intent,
            [operationId.ToString()], "auditor-test");
    }

    public async Task<(Result<OperationAcceptedResponse> Result, Uuid CommandId)> ResolveAsync(Uuid requestId)
    {
        var request = await RequestAsync(requestId);
        var command = new ResolveAuditorRequest(Uuid.CreateVersion4(), TenantId, ProgramId, requestId,
            "The delivered responses were accepted.", request.Revision,
            (await RequestHistoryAsync(requestId)).Responses.Select(static response => response.Operation.LogicalId).ToArray());
        return (await Handler<ResolveAuditorRequestHandler>().HandleAsync(Context(command), CancellationToken.None),
            command.CommandId);
    }

    public async Task<ProcessRecord> FinishResolutionAsync(Uuid operationId)
    {
        var record = await RootAsync(operationId, nameof(AuditorRequestResolutionAccepted));
        var accepted = (AuditorRequestResolutionAccepted)record.Event;
        var intent = await ReadAsync<ClosureIntent>(accepted.Intent);
        var decision = await ReadAsync<AuditorResolutionDecision>(intent.AuthoredDecision);
        var children = await AuditorRequestResolutionProofs.ChildrenAsync(Content, TenantId, intent, CancellationToken.None);
        return await Services.GetRequiredService<ProcessRootReaction>().StartAsync(
            AuditorRequestResolutionEffects.ProcessName, new RecordReactorContext(record), accepted.Intent,
            AuditorRequestResolutionProcess.ResponseKeys(TenantId, decision.ProgramId, intent.ParentId, children),
            "auditor-test");
    }

    async Task<DomainEventRecord> RootAsync(Uuid operationId, params string[] kinds)
    {
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()),
                           EventCursor.Start, CancellationToken.None))
            if (kinds.Contains(record.Event.GetType().Name, StringComparer.Ordinal) &&
                record.Event is INativeOperationFact fact && fact.Operation.RootOperationId == operationId)
                return record;
        throw new InvalidOperationException("The operation has no committed native root.");
    }

    public async Task<Uuid> SeedArtifactAsync(EvidenceInspectionOutcome outcome)
    {
        var id = Uuid.CreateVersion4();
        var bytes = Encoding.UTF8.GetBytes("auditor response evidence");
        var artifact = new EvidenceArtifact(TenantId, id);
        Assert.True(artifact.Register(new EvidenceArtifactContent("Audit evidence", null, "document", "Manual upload",
            Now.AddMinutes(-5), new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), "restricted"),
            Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length,
            ActorReference.ForMember(MemberId, "attest@example.test"), Now.AddMinutes(-5)).IsSuccess);
        Assert.Null(artifact.RecordInspection(outcome, Now.AddMinutes(-1)));
        await Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));
        return id;
    }
    public async Task<(Uuid PopulationId, Uuid IncludedRowId, Uuid ExcludedRowId,
        string ContentSha256)> SeedFrozenPopulationAsync()
    {
        var populationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var includedRowId = Uuid.CreateVersion4();
        var excludedRowId = Uuid.CreateVersion4();
        var memberId = MemberId;
        var population = new AuditPopulation(TenantId, ProgramId, populationId);
        Assert.Null(population.Define(populationId, new AuditPopulationDefinitionContent(
            "Production changes", "Changes in the examination period.",
            "Approved change register", new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31), "Include approved changes.",
            "Exclude canceled changes.", "Reconcile every source row.", ["control"],
            [controlId]), ClosedPeriod(ExaminationId), memberId, "Compliance lead", Now));
        Assert.Null(population.RecordFacts(population.Revision, SourceSnapshot(),
            [Row(includedRowId, "included"), Row(excludedRowId, "excluded")], memberId,
            "Compliance lead", Now.AddMinutes(1)));
        Assert.Null(population.Reconcile(population.Revision,
            new AuditPopulationReconciliationContent(2, 1, 1, 0, 0, 0, 0, 0, 0, 0,
                "Every source row is accounted for."), memberId, "Compliance lead",
            Now.AddMinutes(2)));
        Assert.Null(population.Freeze(population.Revision,
            "The exact source rows and reconciliation were reviewed.", memberId,
            "Compliance lead", Now.AddMinutes(3)));
        var contentSha256 = population.Current!.Current!.ContentSha256;
        await Writer.SaveAsync(population, new RequestDispatchContext(RequestActor.System));
        return (populationId, includedRowId, excludedRowId, contentSha256);
    }

    public async Task<string> AmendPopulationAsync(Uuid populationId, Uuid includedRowId)
    {
        var population = await Reader.HydrateAsync(new AuditPopulation(TenantId,
            ProgramId, populationId));
        var sourceReference = "approved-change-register-v2";
        Assert.Null(population.Amend(population.Revision,
            "The auditor received the corrected approved change register.",
            new AuditPopulationSourceSnapshotContent(Uuid.CreateVersion4(), 2,
                sourceReference, Hash('e'), 2, true,
                "The corrected export includes the same reconciled population.", Now.AddMinutes(4)),
            [Row(includedRowId, "included", sourceReference)], MemberId,
            "Compliance lead", Now.AddMinutes(4)));
        Assert.Null(population.Reconcile(population.Revision,
            new AuditPopulationReconciliationContent(2, 1, 1, 0, 0, 0, 0, 0, 0, 0,
                "The corrected register reconciles to the frozen population."), MemberId,
            "Compliance lead", Now.AddMinutes(5)));
        Assert.Null(population.Freeze(population.Revision,
            "The corrected source was reconciled and reviewed.", MemberId,
            "Compliance lead", Now.AddMinutes(6)));
        var contentSha256 = population.Current!.Current!.ContentSha256;
        await Writer.SaveAsync(population, new RequestDispatchContext(RequestActor.System));
        return contentSha256;
    }

    TypeIIPeriodView ClosedPeriod(Uuid examinationId) => new(TenantId: TenantId,
        ProgramId: ProgramId, PeriodId: Uuid.CreateVersion4(), Status: "closed", Revision: 4,
        ExaminationId: examinationId, TypeIIPlanId: Uuid.CreateVersion4(),
        TypeIIPlanVersion: 1, TypeIIPlanContentSha256: Hash('b'),
        PeriodStart: new DateOnly(2026, 1, 1), PeriodEnd: new DateOnly(2026, 12, 31),
        OpeningSnapshotId: Uuid.CreateVersion4(), OpeningSnapshotSha256: Hash('c'),
        OpeningSourceReferences: [], AcknowledgedOpeningGapIds: [], ExpectedOccurrences: [],
        Amendments: [], CloseValidation: null, CloseApprovals: [],
        CloseSnapshots: [new TypeIIPeriodCloseSnapshotView(Uuid.CreateVersion4(), 1, [],
            [], [], Hash('a'), null, null, [], [], Uuid.CreateVersion4(), "ceo", "CEO", Now)],
        ActorMemberId: Uuid.CreateVersion4(), ActorDisplay: "Compliance lead", UpdatedAt: Now);

    static AuditPopulationSourceSnapshotContent SourceSnapshot() => new(
        Uuid.CreateVersion4(), 1, "approved-change-register", Hash('d'), 2, true,
        "Complete export from the approved change register.", Now.AddMinutes(1));

    static AuditPopulationRowContent Row(Uuid rowId, string classification,
        string sourceReference = "approved-change-register") => new(rowId,
        $"change-{classification}", sourceReference, classification,
        "The row was classified against the approved change register.",
        new Dictionary<string, string> { ["control"] = "AC-1" }, [], []);

    static string Hash(char value) => new(value, 64);

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    sealed class ArtifactPermission(bool allowed) : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(allowed);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(allowed, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            IReadOnlyCollection<AccessGrantScope> scopes, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed && tenantId != Uuid.Empty && userId != Uuid.Empty &&
                memberId == RbacIds.Member(tenantId, userId) && scopes.Count == 1 && scopes.Single() is
                {
                    Kind: AccessGrantScopeKind.SharedResource, ResourceType: EvidenceArtifactResourceTypes.Artifact,
                    Id: var artifactId
                } && artifactId != Uuid.Empty && permission == RbacPermissions.EvidenceArtifactRead);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
