using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
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
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Handlers;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Changes.Tests;

sealed class SignificantChangeNativeFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public SignificantChangeNativeFixture()
    {
        var services = new ServiceCollection();
        Receipts = new ReceiptTrackingEventStore(Events, Clock);
        services.AddSingleton<IEventStore>(Receipts);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        Changes = new SignificantChangeReader(Reader, Events, Content, Receipts);
        Operations = new SignificantChangeOperations(new NativeOperationExecutor(Reader, executor, Content), Changes, Content, Clock);
    }
    public Uuid Tenant { get; } = Uuid.CreateVersion4();
    public Uuid Program { get; } = Uuid.CreateVersion4();
    public Uuid Change { get; } = Uuid.CreateVersion4();
    public Uuid User { get; } = Uuid.CreateVersion4();
    public InMemoryEventStore Events { get; } = new();
    public ReceiptTrackingEventStore Receipts { get; }
    public HookedContent Content { get; } = new(ChangeSeeds.Content());
    public MutableClock Clock { get; } = new();
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public SignificantChangeReader Changes { get; }
    public SignificantChangeOperations Operations { get; }
    public SignificantChangeContent Body(string title = "Production migration") => new(null, null, null, title, "Move the governed service.",
        new DateOnly(2026, 10, 18), null, RbacIds.Member(Tenant, User), [Uuid.CreateVersion4()], "high", "confidential", []);
    public RequestContext<T> Context<T>(T request, Uuid? user = null) where T : IRequestBase => new(request,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss","bdgrz"),new Claim("sub",(user ?? User).ToString()),
            new Claim("name","Change recorder")], "UnitTest")));
    public async Task RecordAsync(SignificantChangeContent? body = null)
    {
        var result = await new RecordSignificantChangeHandler(Operations).HandleAsync(Context(new RecordSignificantChange(Tenant, Program, Change, body ?? Body())), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync()
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(), SignificantChange.Area, Change.ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                facts.Add(record.Event);
        return facts;
    }
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
    internal sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal sealed class HookedContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public Func<Task>? BeforeRead { get; set; }
        public Action? BeforeWrite { get; set; }
        public Func<ImmutableContentRef, Exception?>? Failure { get; set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (BeforeWrite is { } before)
            { BeforeWrite = null; before(); }
            return inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (BeforeRead is { } before)
            {
                BeforeRead = null;
                await before();
                throw new ImmutableContentUnavailableException("The original preparation raced an equal winner and temporarily lost its body.");
            }
            if (Failure?.Invoke(reference) is { } failure)
                throw failure;
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
