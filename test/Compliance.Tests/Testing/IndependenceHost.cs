using System.Security.Claims;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
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
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The production composition over in-memory stores with a current client administrator: every command runs
///     through the real bus, authorizers, handlers, content store and process driver.
/// </summary>
sealed class IndependenceHost : IAsyncDisposable
{
    public static readonly DateTimeOffset At = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public IndependenceHost(bool permitted = true,
        Func<IImmutableContentStore, IImmutableContentStore>? configureContent = null)
    {
        var services = new ServiceCollection();
        var settings = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        };
        ProfessionalDutyAssertionTestIssuer.AddConfiguration(settings);
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<ISourceFactResolver, EventStoreSourceFactResolver>();
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        if (configureContent is not null)
            services.AddSingleton<IImmutableContentStore>(provider => configureContent(
                new FitzKvImmutableContentStore(provider.GetRequiredService<IKvClient>())));
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(permitted));
        services.AddSingleton<IPlatformUserDirectoryReader>(new AnyPlatformUserDirectory());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
        Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Scope = Provider.CreateAsyncScope();
    }

    public Uuid Tenant { get; } = Uuid.CreateVersion4();

    public Uuid User { get; } = Uuid.CreateVersion4();

    public ServiceProvider Provider { get; }

    public AsyncServiceScope Scope { get; }

    public ClaimsPrincipal Actor => ActorFor(User);

    public IRequestBus Bus => Scope.ServiceProvider.GetRequiredService<IRequestBus>();

    public IImmutableContentStore Content => Scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();

    public ActorReference MemberReference => ActorReference.ForMember(RbacIds.Member(Tenant, User),
        "Synthetic client administrator");

    /// <summary>The wire form of a value, for comparing views whose collections compare by reference.</summary>
    public static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value,
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);

    public static ClaimsPrincipal ActorFor(Uuid user) => new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));

    public ValueTask<Result<TResult>> SendAsync<TResult>(IRequest<TResult> request, ClaimsPrincipal? actor = null) =>
        Bus.SendAsync(request, actor ?? Actor);

    /// <summary>Dispatches a request as a personal HTTP call by <paramref name="actor" /> (default: the client administrator).</summary>
    public ValueTask<Result<TResult>> HttpAsync<TResult>(IRequest<TResult> request, ClaimsPrincipal? actor = null) =>
        Bus.DispatchAsync(request, new RequestDispatchContext(actor ?? Actor,
            new HttpInvocation("PUT", "/synthetic/decision", "/synthetic/decision", "synthetic")),
            CancellationToken.None);

    public async Task<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate) where TAggregate : Aggregate =>
        await Scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(aggregate);

    /// <summary>Registers an active staff member of a practice in the firm directory.</summary>
    public Task<FirmStaffMemberView> RegisterStaffAsync(string practice = "advisory") =>
        FirmStaffFacts.RegisterAsync(Provider, Uuid.CreateVersion4(), Uuid.CreateVersion4(), practice,
            "Synthetic staff source", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"),
            At);

    /// <summary>Retains the first draft rule version, as the platform operator would author it.</summary>
    public Task SeedDraftRulesAsync(IndependenceRuleContent? content = null) =>
        ProgramManagementServices.SeedAsync(Provider, new IndependenceRuleCatalogRules(), catalog => catalog.ReviseRules(
            Uuid.CreateVersion4(), catalog.Sequence, content ?? DefaultRules,
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), At));

    public static IndependenceRuleContent DefaultRules { get; } = new(12,
        [new IndependenceServiceRuleContent("design", "impairing", "impairing"),
         new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
        "Synthetic draft source");

    public static NonattestServiceContent Service(string kind = "design", DateOnly? start = null, DateOnly? end = null,
        params Uuid[] staff) => new(Uuid.CreateVersion4(), kind, start ?? new DateOnly(2026, 1, 1), end,
        staff.Length == 0 ? [Uuid.CreateVersion4()] : staff, false, "Synthetic client source");

    public static ServiceEngagementDraftContent Draft(FirmStaffMemberView lead, string scope = "Synthetic scope") =>
        new(lead.Practice, scope, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), lead.StaffMemberId);

    /// <summary>A draft with no end of period, so the effective date of a later assignment never expires.</summary>
    public static ServiceEngagementDraftContent OpenDraft(FirmStaffMemberView lead, string scope = "Synthetic scope") =>
        new(lead.Practice, scope, new DateOnly(2026, 1, 1), null, lead.StaffMemberId);

    public async Task<List<TEvent>> EventsAsync<TEvent>(EventStreamAddress stream) where TEvent : DomainEvent
    {
        var found = new List<TEvent>();
        try
        {
            await foreach (var record in Scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                               .ReadAsync(stream, 0))
                if (record.Event is TEvent typed)
                    found.Add(typed);
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unwritten stream this way.
        }

        return found;
    }

    public Task<ProcessRecord> RunPreviewAsync(Uuid evaluationId) =>
        IndependenceFacts.RunPreviewProcessAsync(Provider, Tenant, evaluationId);

    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        await Provider.DisposeAsync();
    }
}
