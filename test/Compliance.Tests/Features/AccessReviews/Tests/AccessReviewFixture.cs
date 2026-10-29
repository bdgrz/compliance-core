using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.History;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
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
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Authorizers;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     Runs access-review requests through Portia's real lifecycle with the production authorizer,
///     an in-memory event store, the population snapshot primitive, and in-memory Fitz directories.
/// </summary>
sealed class AccessReviewFixture : IAsyncDisposable
{
    public static readonly DateTimeOffset Observed = DateTimeOffset.UtcNow.AddDays(-2);

    AccessReviewFixture(ServiceProvider provider, MemberPermissions permissions,
        FakeAccessReviewSources sources, FakeApplicationDirectory applications,
        bool applicationRestricted, ManualClock clock)
    {
        Clock = clock;
        Provider = provider;
        Permissions = permissions;
        Sources = sources;
        Applications = applications;
        ApplicationRestricted = applicationRestricted;
    }

    public ServiceProvider Provider { get; }
    public ManualClock Clock { get; }
    public MemberPermissions Permissions { get; }
    public FakeAccessReviewSources Sources { get; }
    public FakeApplicationDirectory Applications { get; }
    public bool ApplicationRestricted { get; }
    public Uuid TenantId { get; } = Uuid.CreateVersion4();
    public Uuid ProgramId { get; } = Uuid.CreateVersion4();
    public Uuid ApplicationId { get; } = Uuid.CreateVersion4();
    public Uuid InstanceId { get; } = Uuid.CreateVersion4();
    public Uuid ManagerUserId { get; } = Uuid.CreateVersion4();
    public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
    public Uuid OutsiderUserId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerMemberId => RbacIds.Member(TenantId, ReviewerUserId);
    public Uuid ManagerMemberId => RbacIds.Member(TenantId, ManagerUserId);
    public Uuid ApproverMemberId => RbacIds.Member(TenantId, ApproverUserId);
    public Uuid RemediationOwnerMemberId => ManagerMemberId;
    public Uuid AdaPersonId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerPersonId { get; } = Uuid.CreateVersion4();
    public Uuid BotIdentityId { get; } = Uuid.CreateVersion4();

    public static async Task<AccessReviewFixture> CreateAsync(bool applicationRestricted = false,
        Func<IProcessRecordStore, IProcessRecordStore>? processStore = null)
    {
        var clock = new ManualClock();
        var permissions = new MemberPermissions();
        var sources = new FakeAccessReviewSources();
        var applications = new FakeApplicationDirectory();
        var provider = ProgramManagementServices.Build(permissions,
            portia => portia.AddRequestAuthorizer<AccessReviewAuthorizer>()
                .AddRequestHandler<OpenAccessPopulationHandler>()
                .AddRequestHandler<RecordAccessPopulationFactsHandler>()
                .AddRequestHandler<PreviewAccessPopulationHandler>()
                .AddRequestHandler<AcceptAccessPopulationHandler>()
                .AddRequestHandler<GetAccessPopulationHandler>()
                .AddRequestHandler<ListAccessPopulationsHandler>()
                .AddRequestHandler<ExemptMissingAccessPopulationHandler>()
                .AddRequestHandler<ClassifyAccessPrincipalHandler>()
                .AddRequestHandler<ListAccessPrincipalsHandler>()
                .AddRequestHandler<ProposeAccessExpectationHandler>()
                .AddRequestHandler<ApproveAccessExpectationHandler>()
                .AddRequestHandler<ExemptAccessExpectationHandler>()
                .AddRequestHandler<ListAccessExpectationsHandler>()
                .AddRequestHandler<GetAccessVarianceHandler>()
                .AddRequestHandler<LaunchAccessReviewCampaignHandler>()
                .AddRequestHandler<GetAccessReviewCampaignHandler>()
                .AddRequestHandler<RecordAccessDecisionHandler>()
                .AddRequestHandler<PreviewBulkAccessDecisionHandler>()
                .AddRequestHandler<RecordAccessRemediationChangeHandler>()
                .AddRequestHandler<ReassignAccessReviewResponsibilityHandler>()
                .AddRequestHandler<VerifyAccessRemediationHandler>()
                .AddRequestHandler<ExemptAccessRemediationHandler>()
                .AddRequestHandler<CompleteAccessReviewCampaignHandler>()
                .AddRequestAuthorizer<AccessReviewReceiverAuthorizer>()
                .AddRequestHandler<MaterializeAccessReviewItemHandler>()
                .AddRequestHandler<AuthorizeAccessReviewItemCampaignCompletionUseHandler>()
                .AddRequestHandler<SettleAccessReviewItemCampaignCompletionUseHandler>()
                .AddRequestHandler<CompleteAccessReviewCampaignClosureHandler>()
                .AddRequestHandler<AbortAccessReviewCampaignCompletionHandler>()
                .AddRequestHandler<RecordBulkAccessDecisionHandler>()
                .AddRequestHandler<MaterializeAccessReviewDecisionBatchHandler>()
                .AddRequestHandler<ReserveAccessReviewItemDecisionHandler>()
                .AddRequestHandler<ApplyAccessReviewItemDecisionHandler>()
                .AddRequestHandler<PublishAccessReviewDecisionBatchHandler>()
                .AddRequestHandler<SettleAccessReviewItemDecisionHandler>()
                .AddRequestHandler<FenceAccessReviewItemDecisionHandler>()
                .AddRequestHandler<CompleteAccessReviewDecisionBatchCancellationHandler>()
                .AddRequestHandler<CancelAccessReviewDecisionBatchHandler>(),
            services =>
            {
                var store = new InMemoryEventStore();
                services.AddSingleton<IEventStore>(store);
                services.AddSingleton<IDomainEventReader>(store);
                services.AddScoped<ISourceFactResolver, EventStoreSourceFactResolver>();
                services.AddScoped<PopulationSnapshotFreezer>();
                services.AddScoped<AccessExpectationsReader>();
                services.AddSingleton<AccessReviewManifests>();
                services.AddScoped<AccessReviewReader>();
                services.AddScoped<AccessReviewCompletionVerifier>();
                services.AddScoped<AccessReviewCompletionFences>();
                services.AddScoped<AccessReviewBulkVerifier>();
                services.AddScoped<AccessReviewBulkFences>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IProcessRecordStore>(processStore?.Invoke(new FitzKvProcessRecordStore(
                    new PersistenceKvClient(new InMemoryKvClient()))) ?? new FitzKvProcessRecordStore(
                    new PersistenceKvClient(new InMemoryKvClient())));
                services.AddScoped<ProcessRootReaction>();
                services.AddSingleton(AccessReviewLaunchFanOut.Definition());
                services.AddSingleton(AccessReviewCompletionProcess.Definition());
                services.AddSingleton(BulkAccessDecisionProcess.Definition());
                services.AddSingleton<ProcessCatalog>(provider =>
                    new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
                services.AddScoped<ProcessDriver>();
                services.AddScoped<OperatingAuthority>();
                services.AddScoped<AccessReviewQueueEligibility>();
                services.AddScoped(provider => RestrictedApplicationVisibilityFixture.Create(
                    provider.GetRequiredService<IAggregateReader>(),
                    provider.GetRequiredService<IPermissionAuthorizer>(),
                    provider.GetRequiredService<IApplicationDirectoryReader>()));
                services.AddSingleton<IAccessReviewSources>(sources);
                services.AddSingleton<IApplicationDirectoryReader>(applications);
                services.AddSingleton<IAccessPopulationDirectoryReader>(
                    new FitzAccessPopulationDirectory(new InMemoryKvClient()));
            });
        var fixture = new AccessReviewFixture(provider, permissions, sources, applications,
            applicationRestricted, clock);
        permissions.Allow(fixture.ManagerUserId);
        permissions.Allow(fixture.ApproverUserId);
        permissions.Allow(fixture.ReviewerUserId);
        sources.AccessOwner = fixture.ReviewerMemberId;
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Seeder");
        var now = DateTimeOffset.UtcNow.AddDays(-10);
        applications.Instance = new SystemInstanceView(fixture.TenantId, fixture.ApplicationId,
            fixture.InstanceId, "Production", "aws_account", null, "manual", null, [],
            fixture.ManagerMemberId, "Manager", now)
        {
            Revision = 1,
        };
        await ProgramManagementServices.SeedAsync<DeclaredApplication, ApplicationRegistration>(
            provider, new DeclaredApplication(fixture.TenantId, fixture.ApplicationId),
            application => application.Declare("Payroll", "Run payroll", null,
                fixture.ManagerMemberId, "Manager", now, isRestricted: applicationRestricted));
        await ProgramManagementServices.SeedAsync(provider,
            new DeclaredSystemInstance(fixture.TenantId, fixture.InstanceId), instance =>
                instance.Declare(fixture.ApplicationId, "Production", "aws_account", null, null,
                    Uuid.CreateVersion4(), "Seeder", now));
        await ProgramManagementServices.SeedAsync(provider,
            new ProgramRules(fixture.TenantId, fixture.ProgramId), program =>
                program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    fixture.ManagerMemberId, "Manager", now) is null
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict, "program")));
        foreach (var userId in new[]
                 { fixture.ManagerUserId, fixture.ApproverUserId, fixture.ReviewerUserId })
            await ProgramManagementServices.SeedAsync(provider,
                new Member(fixture.TenantId, userId), member => member.Register());
        foreach (var (personId, name, email) in new[]
                 {
                     (fixture.AdaPersonId, "Ada", "ada@example.com"),
                     (fixture.ReviewerPersonId, "Rae", "rae@example.com"),
                 })
            await ProgramManagementServices.SeedAsync(provider,
                new PersonRules(fixture.TenantId, personId), person => person.Record(name, email, actor, now));
        await ProgramManagementServices.SeedAsync(provider,
            new PersonRules(fixture.TenantId, fixture.ReviewerPersonId), person =>
                person.CorrelateMembership(1, fixture.ReviewerUserId, actor, now) is null
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict, "correlate")));
        await ProgramManagementServices.SeedAsync(provider,
            new ServiceIdentity(fixture.TenantId, fixture.BotIdentityId), identity =>
                identity.Record(new ServiceIdentityTerms("Deploy bot", "bot", "Deploys",
                    null, "active", "person", fixture.AdaPersonId,
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90))),
                    DateOnly.FromDateTime(DateTime.UtcNow), actor, now));
        sources.People =
        [
            Person(fixture, fixture.AdaPersonId, "Ada", "ada@example.com"),
            Person(fixture, fixture.ReviewerPersonId, "Rae", "rae@example.com"),
        ];
        return fixture;
    }

    static PersonView Person(AccessReviewFixture fixture, Uuid personId, string name, string email) =>
        new(fixture.TenantId, personId, 1, name, email, "manual",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Seeder"), DateTimeOffset.UtcNow);

    /// <summary>
    ///     A standard population: Ada (user) is in Engineering, which is in Admins (admin
    ///     privilege), and can assume the Deployer role (deploy permission); Rae (user) holds the
    ///     admin privilege directly; the deploy bot (service principal) holds deploy directly; and
    ///     an IAM user principal holds read access.
    /// </summary>
    public static AccessPopulationFacts StandardFacts(bool raeHasAdmin = true) => new(
        [
            new("ada", "user_account", "Ada", "active", "ada@example.com"),
            new("rae", "user_account", "Rae", "active", "rae@example.com"),
            new("bot", "service_principal", "Deploy bot", "active"),
            new("legacy", "user_account", "Legacy IAM user", "active"),
            new("engineering", "group", "Engineering", "active"),
            new("admins", "group", "Admins", "active"),
            new("deployer", "role", "Deployer", "active"),
        ],
        [
            new("admin", "admin_privilege", "AdministratorAccess"),
            new("deploy", "permission", "Deploy"),
            new("read", "permission", "Read"),
        ],
        [
            new("engineering", "ada"),
            new("admins", "engineering"),
            new("deployer", "ada"),
        ],
        [
            .. raeHasAdmin ? new AccessAssignmentFact[] { new("rae", "admin") } : [],
            new("admins", "admin"),
            new("deployer", "deploy"),
            new("bot", "deploy"),
            new("legacy", "read"),
        ]);

    public RequestScenario As(Uuid userId) => RequestScenario.For(Provider)
        .GivenActor(ProgramManagementServices.Actor(userId));

    public async Task<TOut> SendAsync<TOut>(Uuid userId, IRequest<TOut> request)
    {
        if (PersonalAccessReviewTransportTests.IsPersonalDecision(request))
        {
            var personal = await PersonalAccessReviewTransportTests.SendHttpAsync(Provider, userId, request);
            Assert.True(personal.IsSuccess, personal.Error?.Message);
            return personal.Value;
        }
        var result = await As(userId).When(request).ExpectSuccess();
        return result.Value;
    }

    public Task<RequestError> FailAsync<TOut>(Uuid userId, IRequest<TOut> request,
        RequestErrorKind kind) => FailCoreAsync(userId, request, kind);

    async Task<RequestError> FailCoreAsync<TOut>(Uuid userId, IRequest<TOut> request,
        RequestErrorKind kind)
    {
        if (PersonalAccessReviewTransportTests.IsPersonalDecision(request))
        {
            var personal = await PersonalAccessReviewTransportTests.SendHttpAsync(Provider, userId, request);
            Assert.Equal(kind, personal.Error?.Kind);
            return Assert.IsType<RequestError>(personal.Error);
        }
        var result = await As(userId).When(request).ExpectFailure(kind);
        return Assert.IsType<RequestError>(result.Error);
    }

    /// <summary>Opens, records, and accepts a population; returns its ID and acceptance.</summary>
    public async Task<(Uuid PopulationId, AccessPopulationAcceptance Acceptance)> AcceptAsync(
        AccessPopulationFacts facts, DateTimeOffset? observedAt = null)
    {
        if (ApplicationRestricted)
            Permissions.AllowRestrictedRead(ManagerUserId);
        var opened = await SendAsync(ManagerUserId, Open(observedAt ?? Observed, "AWS console export, attested"));
        var recorded = await SendAsync(ManagerUserId, Record(opened.PopulationId, 1, facts));
        var accepted = await SendAsync(ManagerUserId, new AcceptAccessPopulation(Uuid.CreateVersion4(), TenantId,
            opened.PopulationId, recorded.Revision, "I attest this is the observed population."));
        if (ApplicationRestricted)
            Permissions.DenyRestrictedRead(ManagerUserId);
        return (opened.PopulationId, accepted);
    }

    public OpenAccessPopulation Open(DateTimeOffset observedAt, string source = "Manual export",
        long systemInstanceRevision = 1, Uuid? populationId = null, Uuid? systemInstanceId = null) =>
        new(populationId ?? Uuid.CreateVersion4(), TenantId, ApplicationId, systemInstanceId ?? InstanceId,
            systemInstanceRevision, observedAt, source);

    public RecordAccessPopulationFacts Record(Uuid populationId, long revision, AccessPopulationFacts facts,
        Uuid? commandId = null) =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, populationId, revision, facts.Principals,
            facts.Entitlements, facts.GroupMembers, facts.Assignments);

    public void AllowManagerRestrictedRead() => Permissions.AllowRestrictedRead(ManagerUserId);

    public void DenyManagerRestrictedRead() => Permissions.DenyRestrictedRead(ManagerUserId);

    public ClassifyAccessPrincipal Classify(Uuid populationId, string subject, string classification,
        Uuid? personId = null, Uuid? serviceIdentityId = null, long revision = 0, Uuid? commandId = null) =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, populationId, subject, revision, classification,
            "Reviewed against the roster.", personId, serviceIdentityId);

    public Task<AccessPrincipalClassificationView> ClassifyAsync(Uuid populationId, string subject,
        string classification, Uuid? personId = null, Uuid? serviceIdentityId = null, long revision = 0) =>
        SendAsync(ManagerUserId, Classify(populationId, subject, classification, personId, serviceIdentityId,
            revision));

    /// <summary>Classifies every subject of the standard population.</summary>
    public async Task ClassifyStandardAsync(Uuid populationId)
    {
        await ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Human, AdaPersonId);
        await ClassifyAsync(populationId, "rae", AccessReviewVocabulary.Human, ReviewerPersonId);
        await ClassifyAsync(populationId, "bot", AccessReviewVocabulary.Nhi, serviceIdentityId: BotIdentityId);
        await SendAsync(ManagerUserId, new ClassifyAccessPrincipal(Uuid.CreateVersion4(), TenantId, populationId,
            "legacy", 0, AccessReviewVocabulary.Shared, "Break-glass account.",
            AccountableOwnerPersonId: AdaPersonId, SharedJustification: "Emergency access."));
    }

    public ProposeAccessExpectation Propose(string ruleKind, AccessExpectationParameters parameters,
        long revision = 0, Uuid? expectationId = null, Uuid? supersedes = null, string rationale = "Initial expectation.",
        DateTimeOffset? effectiveFrom = null) =>
        new(expectationId ?? Uuid.CreateVersion4(), TenantId, ApplicationId, InstanceId, revision, ruleKind,
            parameters, rationale, effectiveFrom ?? Observed.AddDays(-30), SupersedesExpectationId: supersedes);

    public ApproveAccessExpectation Approve(Uuid expectationId, long revision, Uuid? commandId = null,
        Uuid? waiverId = null) =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, InstanceId, expectationId, revision,
            waiverId);

    /// <summary>Proposes a root expectation on its own line and approves it independently.</summary>
    public async Task<AccessExpectationView> ApprovedExpectationAsync(string ruleKind,
        AccessExpectationParameters parameters)
    {
        var proposed = await SendAsync(ManagerUserId, Propose(ruleKind, parameters));
        return await SendAsync(ApproverUserId, Approve(proposed.ExpectationId, proposed.Revision));
    }

    public LaunchAccessReviewCampaign LaunchRequest(Uuid populationId, Uuid? reviewerMemberId = null,
        string? delegation = null, Uuid? remediationOwnerMemberId = null, Uuid? campaignId = null) =>
        new(campaignId ?? Uuid.CreateVersion4(), TenantId, "Q3 AWS review",
            "Keep only access each person still needs.", DateTimeOffset.UtcNow.AddDays(14),
            [new AccessReviewAssignment(populationId, reviewerMemberId ?? ReviewerMemberId, delegation)],
            ProgramId, remediationOwnerMemberId ?? RemediationOwnerMemberId);

    /// <summary>Launches a campaign and runs its launch fan-out, so every frozen item is materialized.</summary>
    public async Task<AccessReviewCampaignRegistration> LaunchAsync(Uuid populationId,
        Uuid? reviewerMemberId = null, string? delegation = null, Uuid? remediationOwnerMemberId = null)
    {
        var registration = await SendAsync(ManagerUserId, LaunchRequest(populationId, reviewerMemberId, delegation,
            remediationOwnerMemberId));
        await MaterializeAsync(registration.CampaignId);
        return registration;
    }

    /// <summary>Starts (if absent) and drives the launch fan-out of a launched campaign, as the root reactor would.</summary>
    public async Task<ProcessRecord> MaterializeAsync(Uuid campaignId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var campaign = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new AccessReviewCampaign(TenantId, campaignId));
        var launched = campaign.Launched ?? throw new InvalidOperationException("The campaign was not launched.");
        var manifest = await services.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<AccessReviewItemIntentManifest>(TenantId.ToString(), launched.ItemIntentManifest);
        var records = services.GetRequiredService<IProcessRecordStore>();
        var key = await LaunchKey(campaignId);
        await records.CreateIfAbsentAsync(ProcessRecord.Start(key, NativeFactReference.Of(campaign.Stream, launched),
            launched.Operation.Episode, launched.ItemIntentManifest,
            manifest.Selections.Select(static selection => selection.ItemId.ToString()), DateTimeOffset.UtcNow));
        return await services.GetRequiredService<ProcessDriver>().RunAsync(key, "test-worker",
            new RequestDispatchContext(RequestActor.System));
    }

    public Task<ProcessKey> LaunchKey(Uuid campaignId) =>
        ProcessKeys.FindAsync(Provider, AccessReviewEffects.LaunchFanOutName, TenantId.ToString(), campaignId);

    public Task<ProcessKey> CompletionKey(Uuid commandId) =>
        ProcessKeys.FindAsync(Provider, AccessReviewEffects.CompletionProcessName, TenantId.ToString(), commandId);

    /// <summary>The item facts of one materialized item.</summary>
    public async Task<AccessReviewItem> ItemAsync(Uuid campaignId, Uuid itemId, Uuid? selectedLogicalId = null,
        Uuid selectedBatchId = default, Uuid selectedContentEventId = default) =>
        await ProgramManagementServices.HydrateAsync(Provider, new AccessReviewItem(TenantId, campaignId, itemId,
            selectedLogicalId ?? Uuid.Empty, selectedBatchId, selectedContentEventId));

    public async Task<AccessReviewCampaign> CampaignAggregateAsync(Uuid campaignId, Uuid? selectedLogicalId = null, Uuid selectedRootId = default) =>
        await ProgramManagementServices.HydrateAsync(Provider, new AccessReviewCampaign(TenantId, campaignId,
            selectedLogicalId ?? Uuid.Empty, selectedRootId));

    public async Task<TReactor> BindReactorAsync<TReactor>(TReactor reactor) where TReactor : Reactor
    {
        await new ReactorScenario(new TenantId(TenantId.ToString())).RunAsync(reactor);
        return reactor;
    }

    public Task<AccessReviewCampaignHistorySnapshot> CampaignHistoryAsync(AccessReviewCampaign owner) =>
        AccessReviewCampaignHistory.ReadAsync(Provider.GetRequiredService<IDomainEventReader>(), owner);

    public async Task<AccessReviewCampaignHistorySnapshot> CampaignHistoryAsync(Uuid campaignId) =>
        await CampaignHistoryAsync(await CampaignAggregateAsync(campaignId));

    public Task<AccessReviewItemHistorySnapshot> ItemHistoryAsync(AccessReviewItem owner) =>
        AccessReviewItemHistory.ReadAsync(Provider.GetRequiredService<IDomainEventReader>(), owner);

    public async Task<AccessReviewItemHistorySnapshot> ItemHistoryAsync(Uuid campaignId, Uuid itemId) =>
        await ItemHistoryAsync(await ItemAsync(campaignId, itemId));

    /// <summary>The revision an item command expects: the item's own logical revision.</summary>
    public async Task<long> ItemRevisionAsync(Uuid campaignId, Uuid itemId) =>
        (await ItemAsync(campaignId, itemId)).Revision;

    public RecordAccessDecision Decision(Uuid campaignId, Uuid itemId, long expectedRevision, string decision,
        string rationale, Uuid? waiverId = null, Uuid? commandId = null) => new(commandId ?? Uuid.CreateVersion4(),
        TenantId, campaignId, itemId, expectedRevision, decision, rationale, waiverId);

    public RecordAccessRemediationChange Change(Uuid campaignId, Uuid itemId, long expectedRevision,
        string reference = "ticket-1", string description = "Disable access.", Uuid? commandId = null) =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, campaignId, itemId, expectedRevision, reference,
            description, DateTimeOffset.UtcNow);

    public ExemptAccessRemediation Exception(Uuid campaignId, Uuid itemId, long expectedRevision,
        string rationale = "Compensating control.", DateTimeOffset? expiresAt = null, Uuid? commandId = null) =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, campaignId, itemId, expectedRevision, rationale, expiresAt);

    public VerifyAccessRemediation Verification(Uuid campaignId, Uuid itemId, long expectedRevision,
        Uuid populationId, Uuid? commandId = null) => new(commandId ?? Uuid.CreateVersion4(), TenantId, campaignId,
        itemId, expectedRevision, populationId);

    public CompleteAccessReviewCampaign Completion(Uuid campaignId, long expectedRevision,
        string attestation = "I attest that this review is complete.", Uuid? commandId = null) =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, campaignId, expectedRevision, attestation);

    /// <summary>Starts (if absent) the completion process of an admitted completion, as the root reactor would.</summary>
    public async Task<ProcessRecord> StartCompletionAsync(Uuid campaignId, Uuid commandId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var campaign = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new AccessReviewCampaign(TenantId, campaignId, selectedRootId: commandId));
        var accepted = campaign.AcceptedFor(commandId) ?? throw new InvalidOperationException("Not admitted.");
        var intent = await services.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<ClosureIntent>(TenantId.ToString(), accepted.Intent);
        var targets = await services.GetRequiredService<AccessReviewManifests>()
            .CompletionTargetsAsync(TenantId, campaignId, campaign.Launched!, intent);
        return await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(await CompletionKey(commandId), NativeFactReference.Of(campaign.Stream, accepted),
                accepted.Operation.Episode, accepted.Intent, targets, DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the completion process of an admitted completion.</summary>
    public async Task<ProcessRecord> RunCompletionAsync(Uuid campaignId, Uuid commandId)
    {
        var started = await StartCompletionAsync(campaignId, commandId);
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System));
    }

    /// <summary>Admits a completion and runs its process; returns the admission and the process record.</summary>
    public async Task<(OperationAcceptedResponse Acknowledgment, ProcessRecord Process)> CompleteAsync(
        Uuid userId, Uuid campaignId, long? expectedRevision = null, Uuid? commandId = null,
        string attestation = "I attest that this review is complete.")
    {
        var id = commandId ?? Uuid.CreateVersion4();
        var revision = expectedRevision ?? (await CampaignAggregateAsync(campaignId)).Revision;
        var acknowledgment = await SendAsync(userId, Completion(campaignId, revision, attestation, id));
        return (acknowledgment, await RunCompletionAsync(campaignId, id));
    }

    /// <summary>Dispatches a receiver as the trusted process worker.</summary>
    public async Task<Result<ReceiverOutcome>> ReceiveAsync(IRequest<ReceiverOutcome> command)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(command,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>The receiver command the completion process builds for a step, with the driver's deterministic command id.</summary>
    public async Task<IRequest<ReceiverOutcome>> CompletionStepAsync(ProcessRecord record, string stage, string receiver,
        string? target = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var definition = scope.ServiceProvider.GetRequiredService<ProcessCatalog>()
            .Get(AccessReviewEffects.CompletionProcessName);
        return (await definition.CreateReceiver(new ProcessStep(record, stage, receiver, target,
            AccessReviewCompletionProcess.ReceiverCommandId(record.Source.EventId, receiver, target, record.Episode)),
            scope.ServiceProvider, CancellationToken.None))!;
    }

    public Task<ProcessKey> BulkKey(Uuid decisionBatchId) =>
        ProcessKeys.FindAsync(Provider, AccessReviewEffects.BulkProcessName, TenantId.ToString(), decisionBatchId);

    public async Task<AccessReviewDecisionBatch> BatchAsync(Uuid campaignId, Uuid decisionBatchId,
        Uuid? selectedLogicalId = null) => await ProgramManagementServices.HydrateAsync(Provider,
        new AccessReviewDecisionBatch(TenantId, campaignId, decisionBatchId, selectedLogicalId ?? Uuid.Empty));

    public RecordBulkAccessDecision BulkRequest(Uuid campaignId, IReadOnlyList<Uuid> itemIds, string previewToken,
        long expectedCampaignRevision, string decision = "keep", string rationale = "Still needed.",
        Uuid? decisionBatchId = null) => new(decisionBatchId ?? Uuid.CreateVersion4(), TenantId, campaignId, itemIds,
        decision, rationale, previewToken, expectedCampaignRevision);

    /// <summary>Previews and admits a bulk decision as the reviewer; returns the request and its acknowledgment.</summary>
    public async Task<(RecordBulkAccessDecision Request, OperationAcceptedResponse Acknowledgment)> AdmitBulkAsync(
        Uuid userId, Uuid campaignId, IReadOnlyList<Uuid> itemIds, string decision = "keep",
        string rationale = "Still needed.", Uuid? decisionBatchId = null)
    {
        var preview = await SendAsync(userId, new PreviewBulkAccessDecision(TenantId, campaignId, itemIds, decision));
        var request = BulkRequest(campaignId, itemIds, preview.PreviewToken,
            (await CampaignAggregateAsync(campaignId)).Revision, decision, rationale, decisionBatchId);
        return (request, await SendAsync(userId, request));
    }

    /// <summary>Starts (if absent) the bulk decision process of an admitted batch, as the root reactor would.</summary>
    public async Task<ProcessRecord> StartBulkAsync(Uuid campaignId, Uuid decisionBatchId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var campaign = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new AccessReviewCampaign(TenantId, campaignId, selectedRootId: decisionBatchId));
        var admitted = campaign.AdmittedFor(decisionBatchId) ?? throw new InvalidOperationException("Not admitted.");
        var intent = await services.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<BulkAccessDecisionIntent>(TenantId.ToString(), admitted.Intent);
        var items = await services.GetRequiredService<AccessReviewManifests>()
            .BulkItemsAsync(TenantId.ToString(), intent.Items);
        return await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(await BulkKey(decisionBatchId), NativeFactReference.Of(campaign.Stream, admitted),
                admitted.Operation.Episode, admitted.Intent,
                items.Values.Select(item => AccessReviewCompletionTarget.Of(TenantId, campaignId, item.ItemId)
                    .ToString()), DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the bulk decision process of an admitted batch.</summary>
    public async Task<ProcessRecord> RunBulkAsync(Uuid campaignId, Uuid decisionBatchId)
    {
        var started = await StartBulkAsync(campaignId, decisionBatchId);
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System));
    }

    /// <summary>The receiver command the bulk process builds for a step, with the driver's deterministic command id.</summary>
    public async Task<IRequest<ReceiverOutcome>> BulkStepAsync(ProcessRecord record, string stage, string receiver,
        string? target = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var definition = scope.ServiceProvider.GetRequiredService<ProcessCatalog>()
            .Get(AccessReviewEffects.BulkProcessName);
        return (await definition.CreateReceiver(new ProcessStep(record, stage, receiver, target,
            BulkAccessDecisionProcess.ReceiverCommandId(record.Source.EventId, receiver, target, record.Episode)),
            scope.ServiceProvider, CancellationToken.None))!;
    }

    /// <summary>The receiver command the launch fan-out builds for one item, with the driver's deterministic command id.</summary>
    public async Task<MaterializeAccessReviewItem> MaterializeStepAsync(ProcessRecord record, string target)
    {
        await using var scope = Provider.CreateAsyncScope();
        var definition = scope.ServiceProvider.GetRequiredService<ProcessCatalog>()
            .Get(AccessReviewEffects.LaunchFanOutName);
        return (MaterializeAccessReviewItem)(await definition.CreateReceiver(new ProcessStep(record,
            AccessReviewLaunchFanOut.Materialize, nameof(MaterializeAccessReviewItem), target,
            AccessReviewLaunchFanOut.ReceiverCommandId(record.Source.EventId, target, record.Episode)),
            scope.ServiceProvider, CancellationToken.None))!;
    }

    public async Task<ProcessRecord?> ProcessAsync(ProcessKey key) =>
        await Provider.GetRequiredService<IProcessRecordStore>().ReadAsync(key);

    /// <summary>
    ///     Seeds a campaign that was launched without a routed owner Program, copying the frozen content of an existing
    ///     campaign, and materializes its items.
    /// </summary>
    public async Task<(Uuid CampaignId, Uuid ItemId)> SeedLegacyCampaignAsync(Uuid sourceCampaignId)
    {
        var source = await CampaignAsync(sourceCampaignId);
        var campaignId = Uuid.CreateVersion4();
        var content = Provider.GetRequiredService<IImmutableContentStore>();
        var realm = TenantId.ToString();
        var items = source.Items.Select(item => item.Item with
        {
            ItemId = Uuid.CreateVersion5(campaignId,
            $"{item.Item.PopulationId}\n{item.Item.ProviderSubjectId}\n{item.Item.ProviderEntitlementId}")
        }).ToArray();
        var stream = AccessReviewEffects.CampaignStream(TenantId, campaignId);
        var actor = source.LaunchedBy;
        var provenance = AccessReviewEffects.Root(campaignId, "LaunchAccessReviewCampaign", stream, actor, null,
            new string('c', 64), source.LaunchedAt);
        var header = new DurableIntentHeader(campaignId, stream, stream, stream, actor, "initial", null,
            provenance.AuthoredIntentSha256, source.LaunchedAt);
        var proof = new NativeFactReference(new EventStreamAddress(realm, "population-snapshots",
            source.SnapshotId.ToString()), Uuid.CreateVersion4(), 1);
        var selections = new List<AccessReviewItemSelection>();
        foreach (var item in items.OrderBy(static item => item.ItemId.ToString(), StringComparer.Ordinal))
        {
            var entry = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewContent.ItemEntrySchema, item);
            var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewItemCreationIntent.Schema,
                new AccessReviewItemCreationIntent(header, campaignId, 1, item.ItemId, item.SystemInstanceId, entry,
                    item.ReviewerMemberId, AccessReviewEffects.ReviewerEpisode(campaignId, item.ItemId), proof));
            selections.Add(new AccessReviewItemSelection(item.ItemId, intent));
        }

        var reviewers = await content.WriteJsonAsync<IReadOnlyList<AccessReviewerView>>(realm, Uuid.CreateVersion4(),
            AccessReviewContent.ReviewersSchema, source.Reviewers);
        var itemsRef = await content.WriteJsonAsync<IReadOnlyList<AccessReviewItemView>>(realm, Uuid.CreateVersion4(),
            AccessReviewContent.ItemsSchema, items);
        var manifest = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewItemIntentManifest.Schema,
            new AccessReviewItemIntentManifest(selections));
        var (response, result) = await AccessReviewContent.FreezeOkAsync(content, TenantId,
            AccessReviewContent.CampaignRegistrationSchema, new AccessReviewCampaignRegistration(campaignId, source.SnapshotId,
                source.ContentSha256, items.Length), CancellationToken.None);
        var firstResponse = await FirstResponses.FreezeAsync(content, realm, response);
        await ProgramManagementServices.SeedAsync(Provider, new AccessReviewCampaign(TenantId, campaignId, campaignId),
            campaign => campaign.Launch(provenance, source.Name, source.Instructions, source.Deadline, source.SnapshotId,
                source.ContentSha256, reviewers, itemsRef, manifest, source.LaunchedBy, source.LaunchedAt, null, null,
                firstResponse, result));
        await MaterializeAsync(campaignId);
        var itemId = Assert.Single(items, static item => item.ProviderSubjectId == "ada" &&
                                                         item.ProviderEntitlementId == "deploy").ItemId;
        return (campaignId, itemId);
    }

    public Task<AccessReviewCampaignView> CampaignAsync(Uuid campaignId, Uuid? userId = null) =>
        SendAsync(userId ?? ManagerUserId, new GetAccessReviewCampaign(TenantId, campaignId));

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    /// <summary>A clock the test advances; every handler of the fixture reads it.</summary>
    public sealed class ManualClock : TimeProvider
    {
        TimeSpan _offset = TimeSpan.Zero;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + _offset;

        public void Advance(TimeSpan by) => _offset += by;
    }

    public sealed class FakeApplicationDirectory : IApplicationDirectoryReader
    {
        public SystemInstanceView? Instance { get; set; }

        public ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
            CancellationToken ct = default) => ValueTask.FromResult(Instance is { } instance &&
                instance.TenantId == tenantId && instance.SystemInstanceId == instanceId
                ? instance
                : null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid applicationId, long revision, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
