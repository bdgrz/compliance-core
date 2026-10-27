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
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Workforce.Rules;
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
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.PolicyDistribution.Reactors;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The full composition over in-memory stores for the campaign admission, participant and closure processes:
///     requests run through the production pipeline as personal HTTP callers, root facts are turned into process
///     records by the production reactors, and records are driven by the production driver.
/// </summary>
sealed class CampaignHost : IAsyncDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    readonly ServiceProvider _provider;

    public CampaignHost(Func<IProcessRecordStore, IProcessRecordStore>? store = null,
        IImmutableContentStore? content = null)
    {
        Clock = new ManualClock(Start);
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        if (content is not null)
            services.AddSingleton(content);
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(true)));
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        services.AddSingleton<TimeProvider>(Clock);
        if (store is not null)
        {
            services.RemoveAll<IProcessRecordStore>();
            services.AddSingleton<IProcessRecordStore>(provider =>
                store(new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>())));
        }

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Scope = _provider.CreateAsyncScope();
    }

    public ManualClock Clock { get; }

    public IServiceProvider Provider => _provider;

    public AsyncServiceScope Scope { get; }

    public Uuid Tenant { get; } = Uuid.CreateVersion4();

    public Uuid Program { get; } = Uuid.CreateVersion4();

    public Uuid Manager { get; } = Uuid.CreateVersion4();

    public Uuid ManagerMember => RbacIds.Member(Tenant, Manager);

    public Uuid Learner { get; } = Uuid.CreateVersion4();

    public Uuid LearnerPerson { get; } = Uuid.CreateVersion4();

    public Uuid OfflinePerson { get; } = Uuid.CreateVersion4();

    public ActorReference ManagerActor => ActorReference.ForMember(ManagerMember, "Client manager");

    public IProcessRecordStore Records => Scope.ServiceProvider.GetRequiredService<IProcessRecordStore>();

    public IImmutableContentStore Content => Scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();

    public IDomainEventReader Events => Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();

    /// <summary>Seeds the program and the two people: a member-correlated learner and a person without a membership.</summary>
    public async Task SeedAsync()
    {
        await ProgramManagementServices.SeedAsync(_provider, new ProgramRules(Tenant, Program), program =>
        {
            Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null), Manager,
                "Client manager", Start));
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(_provider, new PersonRules(Tenant, LearnerPerson), person =>
        {
            Assert.True(person.Record("Learner", null, ManagerActor, Start).IsSuccess);
            Assert.Null(person.CorrelateMembership(1, Learner, ManagerActor, Start));
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(_provider, new PersonRules(Tenant, OfflinePerson), person =>
        {
            Assert.True(person.Record("Offline", null, ManagerActor, Start).IsSuccess);
            return Result.Success;
        });
    }

    public static CampaignSubject PolicySubject { get; } = new("policy", Uuid.CreateVersion4(), "POL-1",
        "Access Control Policy", 1, new string('a', 64));

    public static CampaignSubject TrainingSubject { get; } = new("training", Uuid.CreateVersion4(), "TR-1",
        "Security awareness", 1, new string('b', 64));

    /// <summary>Launches a campaign over the given people through the owner path, at the host clock.</summary>
    public async Task<Uuid> LaunchAsync(CampaignSubject? subject = null, params Uuid[] people)
    {
        people = people.Length == 0 ? [LearnerPerson, OfflinePerson] : people;
        var campaignId = Uuid.CreateVersion4();
        var matches = people.ToDictionary(static person => person, person => new RosterMatch(person,
            "Person " + person.ToString()[..4], "employee", null, new DateOnly(2025, 1, 1)));
        await CampaignSeed.LaunchAsync(_provider, Tenant, Program, campaignId, subject ?? PolicySubject,
            PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(), new string('d', 64),
            new RosterAudience(matches, new HashSet<Uuid>(people)), Today.AddDays(30), "Read and acknowledge",
            ManagerActor, ManagerMember, Clock.GetUtcNow());
        return campaignId;
    }

    public DateOnly Today => DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime);

    public static AcknowledgePolicy Acknowledgement(Uuid tenant, Uuid program, Uuid campaign, Uuid person,
        long expectedRevision, Uuid? commandId = null, CampaignSubject? subject = null) => new(
        commandId ?? Uuid.CreateVersion4(), tenant, program, campaign, person, (subject ?? PolicySubject).Version,
        (subject ?? PolicySubject).ContentSha256, PolicyDistributionCampaign.DefaultAcknowledgementText,
        expectedRevision);

    public AcknowledgePolicy Acknowledge(Uuid campaign, Uuid person, long expectedRevision, Uuid? commandId = null) =>
        Acknowledgement(Tenant, Program, campaign, person, expectedRevision, commandId);

    /// <summary>Sends the request as a personal HTTP caller, in its own scope as a concurrent request would.</summary>
    public async Task<Result<T>> SendAsync<T>(IRequest<T> request, Uuid? user = null, RequestMetadata? metadata = null)
        where T : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user ?? Manager),
                new HttpInvocation("PUT", "/synthetic/campaign", "/synthetic/campaign", "synthetic"), metadata),
            CancellationToken.None);
    }

    public async Task<CampaignState> StateAsync(Uuid campaignId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var state = await new CampaignSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>()).ReadAsync(Tenant, Program, campaignId,
            CancellationToken.None);
        Assert.True(state.IsSuccess, state.Error?.Message);
        return state.Value;
    }

    public Task<PolicyCampaignParticipantSnapshot> ParticipantAsync(Uuid campaignId, Uuid person, ReservationKey? selectedUse = null) =>
        CampaignSeed.ParticipantAsync(_provider, Tenant, campaignId, person, selectedUse);

    public async Task<PolicyCampaignSnapshot> CampaignAsync(Uuid campaignId) =>
        await Scope.ServiceProvider.GetRequiredService<PolicyCampaignReader>().ReadAsync(Tenant, campaignId);

    public EventStreamAddress CampaignStream(Uuid campaignId) =>
        new PolicyDistributionCampaign(Tenant, campaignId).Stream;

    public ProcessDefinition Definition(string name) => _provider.GetRequiredService<ProcessCatalog>().Get(name);

    public Task<ProcessKey> ResultKey(Uuid operationId) =>
        ProcessKeys.FindAsync(_provider, Bdgrz.Compliance.Features.PolicyDistribution.Processes.IndependentChildMaterialization.ProcessName, Tenant.ToString(), operationId);

    public Task<ProcessKey> ClosureKey(Uuid operationId) =>
        ProcessKeys.FindAsync(_provider, PolicyCampaignClosureEffects.ProcessName, Tenant.ToString(), operationId);

    /// <summary>
    ///     Runs the participant result root reactor over the committed admissions of a campaign. With
    ///     <paramref name="tolerateCrash" /> an armed <c>CrashAfterReplaces</c> leaves each record created but undriven.
    /// </summary>
    public async Task ReactResultsAsync(Uuid campaignId, bool tolerateCrash = false)
    {
        await using var scope = _provider.CreateAsyncScope();
        var reactor = scope.ServiceProvider.GetRequiredService<PolicyParticipantResultReactor>();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(CampaignStream(campaignId), 0))
            if (record.Event is PolicyParticipantResultAdmitted)
                try
                {
                    await reactor.HandleAsync(
                        AdministratorTransitionHost.ReactionOf<PolicyParticipantResultAdmitted>(record),
                        CancellationToken.None);
                }
                catch (InvalidOperationException) when (tolerateCrash)
                {
                    // The held driver stopped after the process record was created.
                }
    }

    /// <summary>Runs the closure root reactor over the committed closure acceptances of a campaign.</summary>
    public async Task ReactClosureAsync(Uuid campaignId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var reactor = scope.ServiceProvider.GetRequiredService<PolicyCampaignClosureReactor>();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(CampaignStream(campaignId), 0))
            if (record.Event is PolicyCampaignClosureAccepted)
                await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<PolicyCampaignClosureAccepted>(record),
                    CancellationToken.None);
    }

    /// <summary>Drives a process record to its end (or its next wait) with a fresh worker scope.</summary>
    public async Task<ProcessRecord> RunAsync(ProcessKey key, string worker = "test-worker")
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(key, worker,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    public async Task<ProcessRecord?> RecordAsync(ProcessKey key) => await Records.ReadAsync(key);

    public ValueTask<Result<ReceiverOutcome>> DispatchAsync(IRequest<ReceiverOutcome> command) =>
        Scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(command,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);

    /// <summary>The receiver command a step of a record builds, with the deterministic command id of the driver.</summary>
    public async Task<IRequest<ReceiverOutcome>?> StepAsync(ProcessRecord record, string stage, string receiver,
        string? target = null)
    {
        var definition = Definition(record.Key.ProcessKind);
        return await definition.CreateReceiver(new ProcessStep(record, stage, receiver, target,
            EffectIdentity.CommandId(record.Source.EventId, $"{record.Key.ProcessKind}:{receiver}", target ?? "-",
                record.Episode)), Scope.ServiceProvider, CancellationToken.None);
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

    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    public sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
