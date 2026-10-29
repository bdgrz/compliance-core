using System.Security.Claims;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes the five tenant identity-provider handlers over an in-memory event store and content store. A test
///     configures, tests, rotates and disables a connection exactly as the personal HTTP actor and the named test
///     worker would, and reads the stream back to see what facts and content were frozen.
/// </summary>
sealed class TenantIdentityProviderHost : IAsyncDisposable
{
    public const string Reference = "vault://tenant/idp/credential-v1";
    public const string NextReference = "vault://tenant/idp/credential-v2";

    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;

    public TenantIdentityProviderHost(DateTimeOffset? now = null)
    {
        Clock = new MutableClock(now ?? new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        var store = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        services.AddScoped<NativeOperationExecutor>();
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        Content = _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var operations = _scope.ServiceProvider.GetRequiredService<NativeOperationExecutor>();
        Configure = new ConfigureTenantIdentityProviderHandler(operations, Content, Clock);
        Test = new TestTenantIdentityProviderHandler(operations, Content, Clock);
        Record = new RecordTenantIdentityProviderTestResultHandler(operations, Content, Clock);
        Rotate = new RotateTenantIdentityProviderCredentialsHandler(operations, Content, Clock);
        Disable = new DisableTenantIdentityProviderHandler(operations, Content, Clock);
    }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public Uuid ProviderId { get; } = Uuid.CreateVersion4();

    public Uuid UserId { get; } = Uuid.CreateVersion4();

    public MutableClock Clock { get; }

    public IAggregateReader Reader { get; }

    public IDomainEventReader Events { get; }

    public IImmutableContentStore Content { get; }

    public ConfigureTenantIdentityProviderHandler Configure { get; }

    public TestTenantIdentityProviderHandler Test { get; }

    public RecordTenantIdentityProviderTestResultHandler Record { get; }

    public RotateTenantIdentityProviderCredentialsHandler Rotate { get; }

    public DisableTenantIdentityProviderHandler Disable { get; }

    public Uuid MemberId => RbacIds.Member(TenantId, UserId);

    public ConfigureTenantIdentityProvider ConfigureCommand(Uuid? commandId = null, string reference = Reference,
        long expectedRevision = 0) => new(commandId ?? Uuid.CreateVersion4(), TenantId, ProviderId,
        "https://idp.example.test", "client-app", reference, ["client-app", "api://compliance"], expectedRevision);

    public RotateTenantIdentityProviderCredentials RotateCommand(long expectedRevision, Uuid? commandId = null,
        string reference = NextReference, string rotationReference = "rotation-2026-10") =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, ProviderId, reference, rotationReference, expectedRevision);

    public async Task<TenantIdentityProvider> ProviderAsync() =>
        await Reader.HydrateAsync(new TenantIdentityProvider(TenantId, ProviderId));

    public async Task<Result> ConfigureAsync(ConfigureTenantIdentityProvider? command = null) =>
        await Configure.HandleAsync(Context(command ?? ConfigureCommand()), CancellationToken.None);

    /// <summary>Requests a test and returns the persisted request event id.</summary>
    public async Task<Uuid> RequestTestAsync(string testKey, long expectedRevision)
    {
        var result = await Test.HandleAsync(Context(new TestTenantIdentityProvider(TenantId, ProviderId, expectedRevision,
            testKey)), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return await FindTestRequestEventIdAsync(testKey);
    }

    /// <summary>Records a result as the named worker, caused by the exact request.</summary>
    public async Task<Result> RecordResultAsync(Uuid requestEventId, long expectedRevision,
        TenantIdentityProviderTestResultContent result, DateTimeOffset? completedAt = null, Uuid? commandId = null) =>
        await Record.HandleAsync(WorkerContext(new RecordTenantIdentityProviderTestResult(
            commandId ?? RecordTenantIdentityProviderTestResultHandler.CommandIdOf(requestEventId, TenantId, ProviderId),
            TenantId, ProviderId, expectedRevision, requestEventId, result, completedAt ?? Clock.GetUtcNow()),
            requestEventId), CancellationToken.None);

    public async Task<List<DomainEvent>> ReadEventsAsync()
    {
        var events = new List<DomainEvent>();
        try
        {
            await foreach (var record in Events.ReadAsync(new TenantIdentityProvider(TenantId, ProviderId).Stream, 0,
                               CancellationToken.None))
                events.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return events;
    }

    public async Task<Uuid> FindTestRequestEventIdAsync(string testKey)
    {
        await foreach (var record in Events.ReadAsync(new TenantIdentityProvider(TenantId, ProviderId).Stream, 0,
                           CancellationToken.None))
        {
            if (record.Event is TenantIdentityProviderTestRequested requested && requested.TestKey == testKey)
                return record.Event.Metadata.EventId;
        }

        throw new InvalidOperationException("The provider test request event was not persisted.");
    }

    public Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid? userId = null) where T : IRequestBase
    {
        var path = request switch
        {
            ConfigureTenantIdentityProvider => "/api/v1/tenants/test/identity-providers/test/command",
            TestTenantIdentityProvider => "/api/v1/tenants/test/identity-providers/test/tests/key",
            RotateTenantIdentityProviderCredentials =>
                "/api/v1/tenants/test/identity-providers/test/credential-rotations/command",
            _ => "/api/v1/tenants/test/identity-providers/test/disable/command",
        };
        return new(request, new RequestDispatchContext(Actor(userId ?? UserId),
            new HttpInvocation("PUT", path, path, "identity-provider-test")));
    }

    public static Cntryl.Portia.RequestContext<RecordTenantIdentityProviderTestResult> WorkerContext(
        RecordTenantIdentityProviderTestResult request, Uuid causationId, ClaimsPrincipal? actor = null,
        RequestInvocation? invocation = null) => new(request,
        new RequestDispatchContext(actor ?? RequestActor.CreateSystem(
                RecordTenantIdentityProviderTestResultAuthorizer.ProcessId, "bdgrz.system"),
            invocation ?? new DirectInvocation(),
            new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), causationId)));

    public static TenantIdentityProviderTestResultContent Passed(string testKey) => new(testKey, "passed", null, null);

    public static TenantIdentityProviderTestResultContent Failed(string testKey) =>
        new(testKey, "failed", "invalid_client", "The provider rejected the configured client credentials.");

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "administrator@example.test")], "BdgrzSession"));

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    /// <summary>A clock a test moves forward.</summary>
    public sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
