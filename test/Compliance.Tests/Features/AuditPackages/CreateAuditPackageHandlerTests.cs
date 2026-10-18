using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackage;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class CreateAuditPackageHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCreatePackageFromExactFrozenTypeIBaselineGivenPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, baseline);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            command), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.Current!.Status);
        Assert.Equal(baseline.TypeIBaselineId, saved.Current.Versions[0].SourceSnapshotId);
        Assert.Equal(baseline.ContentSha256, saved.Current.Versions[0].SourceSnapshotSha256);
        var selected = Assert.Single(saved.Current.Versions[0].SelectedContent);
        Assert.Equal(fixture.SourceRecordId, selected.SourceRecordId);
        Assert.Equal(fixture.SourceRevision, selected.SourceVersion);
        Assert.Equal(fixture.SourceSha256, selected.ContentSha256);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId),
            saved.Current.CreatedByMemberId);
    }

    [Fact]
    public async Task ShouldRejectSelectionOutsideFrozenBaselineGivenCreateCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, baseline) with
        {
            Content = fixture.Content(baseline) with
            {
                SelectedContent = [fixture.Selection() with
                {
                    SourceRecordId = Uuid.CreateVersion4(),
                }],
            },
        };

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            command), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectTypeIIPackageUntilCloseSnapshotResolutionIsSupportedGivenCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, await fixture.SeedTypeIBaselineAsync()) with
        {
            Content = fixture.Content(await fixture.GetBaselineAsync()) with
            {
                PackageKind = "type_ii",
                SourceSnapshotKind = "type_ii_close_snapshot",
            },
        };

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            command), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenCreateCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var command = fixture.Command(Uuid.CreateVersion4(), baseline);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.SystemContext(command),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);
        TypeIBaselineView? _baseline;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid SourceRecordId { get; } = Uuid.CreateVersion4();
        public Uuid SourceVersionId { get; } = Uuid.CreateVersion4();
        public string SourceSha256 { get; } = new('b', 64);
        public long SourceRevision { get; } = 3;
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public CreateAuditPackageHandler Handler() => new(Executor,
            new AuditPackageSourceBuilder(Reader), _clock);

        public CreateAuditPackage Command(Uuid packageId, TypeIBaselineView baseline) =>
            new(TenantId, ProgramId, packageId, Content(baseline));

        public AuditPackageContent Content(TypeIBaselineView baseline) => new(ExaminationId,
            "type_i", "type_i_baseline", baseline.TypeIBaselineId, baseline.ContentSha256,
            "SOC 2 Type I", [Selection()]);

        public AuditPackageSelectionEntry Selection() => new("system_description",
            "system-description-1", "system_description", SourceRecordId, SourceRevision,
            "json", SourceSha256, 128, "management");

        public async Task<TypeIBaselineView> SeedTypeIBaselineAsync()
        {
            var engagement = new ExaminationEngagement(TenantId, ProgramId, ExaminationId);
            var memberId = RbacIds.Member(TenantId, UserId);
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i",
                "Independent Audit LLP", new DateOnly(2026, 9, 30), null, ["security"],
                Uuid.CreateVersion4(), [memberId]), memberId, "Engagement lead", Now));
            var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, ExaminationId,
            [new TypeIBaselineSourceReference("system_description", SourceRecordId,
                SourceVersionId, SourceRevision, SourceSha256)]);
            Assert.Null(engagement.FreezeBaseline(engagement.Revision, BaselineId, preview,
                "Confirmed the exact Type I source snapshot.", memberId, "Engagement lead",
                Now.AddMinutes(1)));
            _baseline = engagement.Baseline;
            await Writer.SaveAsync(engagement, new RequestDispatchContext(RequestActor.System));
            return _baseline!;
        }

        public Task<TypeIBaselineView> GetBaselineAsync() => Task.FromResult(_baseline!);

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
                new HttpInvocation("POST", "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "audit-package-create-test")));

        public static Cntryl.Portia.RequestContext<T> SystemContext<T>(T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "audit-package-create-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "package-author@example.test")], "BdgrzSession"));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
