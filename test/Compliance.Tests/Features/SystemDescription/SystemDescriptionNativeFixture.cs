using System.Security.Claims;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using DescriptionOwner = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

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
