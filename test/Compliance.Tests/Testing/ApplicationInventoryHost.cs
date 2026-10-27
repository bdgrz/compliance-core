using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The production handler composition for the declared application and system instance operations over
///     in-memory stores. Every call is its own request context with an explicit actor, so tests choose whether a
///     retry comes from the same member or from another one.
/// </summary>
sealed class ApplicationInventoryHost : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;

    public ApplicationInventoryHost(bool governedRevisions = false)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
            ["Compliance:Applications:GovernedRevisionsEnabled"] = governedRevisions.ToString(),
        }).Build(), developerAuthentication: true);
        var store = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IAuthoritativeSnapshotReader>(new EventStoreSnapshotReader(store));
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddScoped(provider => RestrictedApplicationVisibilityFixture.Create(
            provider.GetRequiredService<IAggregateReader>(), organizationPermission: true));
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Tenant = Uuid.CreateVersion4();
        Actor = ActorFor(Uuid.CreateVersion4());
    }

    public Uuid Tenant { get; }

    /// <summary>The member who authors operations unless a test names another one.</summary>
    public ClaimsPrincipal Actor { get; }

    public IServiceProvider Services => _scope.ServiceProvider;

    public IAggregateReader Reader => Services.GetRequiredService<IAggregateReader>();

    public IAggregateWriter Writer => Services.GetRequiredService<IAggregateWriter>();

    public IDomainEventReader Events => Services.GetRequiredService<IDomainEventReader>();

    public DeclareApplicationHandler Declare => Create<DeclareApplicationHandler>();

    public ReviseApplicationHandler Revise => Create<ReviseApplicationHandler>();

    public RetireApplicationHandler Retire => Create<RetireApplicationHandler>();

    public DeclareSystemInstanceHandler DeclareInstance => Create<DeclareSystemInstanceHandler>();

    public ReviseSystemInstanceHandler ReviseInstance => Create<ReviseSystemInstanceHandler>();

    public RetireSystemInstanceHandler RetireInstance => Create<RetireSystemInstanceHandler>();

    public static ClaimsPrincipal ActorFor(Uuid userId, string display = "Inventory manager") => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("name", display)],
        "BdgrzSession"));

    public THandler Create<THandler>() where THandler : class =>
        ActivatorUtilities.CreateInstance<THandler>(Services);

    public InventoryRequestContext<T> Context<T>(T request, ClaimsPrincipal? actor = null) where T : notnull =>
        new(request, actor ?? Actor);

    public Task<T> HydrateAsync<T>(T aggregate) where T : Aggregate => Reader.HydrateAsync(aggregate).AsTask();

    public Task<DeclaredApplication> ApplicationAsync(Uuid applicationId) =>
        HydrateAsync(new DeclaredApplication(Tenant, applicationId));

    public Task<DeclaredSystemInstance> InstanceAsync(Uuid instanceId) =>
        HydrateAsync(new DeclaredSystemInstance(Tenant, instanceId));

    public Task<List<DomainEventRecord>> ApplicationEventsAsync(Uuid applicationId) =>
        StreamAsync(DeclaredApplication.Area, applicationId);

    public Task<List<DomainEventRecord>> InstanceEventsAsync(Uuid instanceId) =>
        StreamAsync(DeclaredSystemInstance.Area, instanceId);

    public async Task<Uuid> RecordPersonAsync(string displayName)
    {
        var personId = Uuid.CreateVersion4();
        var person = new PersonRules(Tenant, personId);
        Assert.True(person.Record(displayName, null, ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now)
            .IsSuccess);
        await PersonSeeds.AppendAsync(_scope.ServiceProvider, person);
        return personId;
    }

    /// <summary>Declares an application through the handler and returns its identity.</summary>
    public async Task<Uuid> DeclareApplicationAsync(Uuid? applicationId = null, ClaimsPrincipal? actor = null,
        string name = "Payroll", string? classification = null, Uuid? owner = null)
    {
        var id = applicationId ?? Uuid.CreateVersion4();
        var result = await Declare.HandleAsync(Context(new DeclareApplication(id, Tenant, name, "Run payroll",
            "Finance", classification, owner, owner), actor), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return id;
    }

    async Task<List<DomainEventRecord>> StreamAsync(string area, Uuid resource)
    {
        var records = new List<DomainEventRecord>();
        try
        {
            await foreach (var record in Events.ReadAsync(new EventStreamAddress(Tenant.ToString(), area,
                               resource.ToString()), 0, CancellationToken.None))
                records.Add(record);
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unwritten stream this way.
        }

        return records;
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
