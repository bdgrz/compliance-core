using Bdgrz.Compliance.Features.SourceFacts;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The production composition over in-memory stores with a current client administrator: every command runs
///     through the real bus, authorizers, handlers, content store and process driver.
/// </summary>
sealed class IndependenceHost : IAsyncDisposable
{
    public static readonly DateTimeOffset At = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public IndependenceHost(bool permitted = true)
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
