using System.Security.Claims;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using DescriptionOwner = Bdgrz.Compliance.Features.SystemDescription.Aggregates.SystemDescription;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Contracts;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
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
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;

sealed class SystemDescriptionNativeFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    readonly string _directory = Path.Combine(Path.GetTempPath(), "bdgrz-description-native-" + Guid.NewGuid().ToString("N"));
    public SystemDescriptionNativeFixture()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        Descriptions = new SystemDescriptionReader(Reader, Events, Content);
        Operations = new SystemDescriptionOperations(new NativeOperationExecutor(Reader, Executor, Content), Descriptions, Content, Clock);
        Processes = new FitzKvSystemDescriptionExportProcesses(Kv);
        Artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(_directory, new byte[32], ArtifactContentLimits.MaxContentLength), Clock);
        Exports = new SystemDescriptionExportProcess(Processes, Descriptions, Content, Artifacts, Clock);
    }
    public Uuid Tenant { get; } = Uuid.CreateVersion4();
    public Uuid Program { get; } = Uuid.CreateVersion4();
    public Uuid Description { get; } = Uuid.CreateVersion4();
    public Uuid Author { get; } = Uuid.CreateVersion4();
    public Uuid Reviewer { get; } = Uuid.CreateVersion4();
    public Uuid Approver { get; } = Uuid.CreateVersion4();
    public InMemoryEventStore Events { get; } = new();
    public InMemoryKvClient Kv { get; } = new();
    public IImmutableContentStore Content { get; } = DescriptionSeeds.Content();
    public MutableClock Clock { get; } = new();
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public SystemDescriptionReader Descriptions { get; }
    public SystemDescriptionOperations Operations { get; }
    public ISystemDescriptionExportProcesses Processes { get; }
    public IArtifactContentStore Artifacts { get; }
    public SystemDescriptionExportProcess Exports { get; }
    public Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid? user = null) where T : IRequestBase => new(request,
        new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss","bdgrz"),new Claim("sub",(user ?? Author).ToString()),
            new Claim("name","Description member")], "UnitTest")), new HttpInvocation("PUT", "/description", "/description", "description-test")));
    public async Task<SystemDescriptionRules> SeedAsync(bool approved = false, SystemDescriptionDraftContent? content = null, Uuid? id = null)
    {
        var rules = new SystemDescriptionRules(Tenant, Program, id ?? Description);
        Assert.Null(rules.Create(Uuid.CreateVersion4(), new SystemDescriptionScope("type_i", Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, null),
            content ?? Body(), RbacIds.Member(Tenant, Author), "Author", Clock.Now));
        if (approved)
        {
            Assert.Null(rules.Review(1, RbacIds.Member(Tenant, Reviewer), "Reviewer", "approve", "Reviewed exact source", Clock.Now.AddMinutes(1)));
            Assert.Null(rules.Approve(1, RbacIds.Member(Tenant, Approver), "Approver", "Approved exact source", Clock.Now.AddMinutes(2)));
        }
        await DescriptionSeeds.WriteAsync(Writer, Content, rules);
        Clock.Now = Clock.Now.AddMinutes(3);
        return rules;
    }
    static readonly string[] SectionIds = new[] {"services","commitments_requirements","components_infrastructure_software","people","procedures","information_data_flows",
            "system_boundary","control_environment","user_entity_controls","subservice_organizations","exclusions","relevant_changes"};
    public static SystemDescriptionDraftContent Body(string title = "Approved narrative") => new(title,
        SectionIds.Select(id => new SystemDescriptionSection(id, id.Replace('_', ' '), "Exact narrative for " + id, "complete", null)).ToArray(), [], [], []);
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid? description = null)
    {
        var result = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(), DescriptionOwner.Area, (description ?? Description).ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                result.Add(record.Event);
        return result;
    }
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
    public sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
