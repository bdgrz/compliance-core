using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

public sealed class SystemDescriptionCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReviewApproveBindAndExportExactVersionGivenIndependentActors()
    {
        // Arrange
        await using var fixture = new Fixture();
        var descriptionId = await fixture.SeedDescriptionAsync(typeII: false);

        // Act
        var review = await fixture.ReviewHandler().HandleAsync(Fixture.Context(
            fixture.ReviewerId, new ReviewSystemDescription(fixture.TenantId, fixture.ProgramId,
                descriptionId, 1, "approve", "Required sections and current sources reviewed.")),
            CancellationToken.None);
        var approval = await fixture.ApproveHandler().HandleAsync(Fixture.Context(
            fixture.ApproverId, new ApproveSystemDescription(fixture.TenantId, fixture.ProgramId,
                descriptionId, 1, "Management approves the current system description.")),
            CancellationToken.None);
        var binding = await fixture.BindHandler().HandleAsync(Fixture.Context(fixture.ApproverId,
            new BindSystemDescriptionToExamination(fixture.TenantId, fixture.ProgramId,
                descriptionId, 1, fixture.ExaminationId, "type_i_baseline")), CancellationToken.None);
        var export = await fixture.ExportHandler().HandleAsync(Fixture.Context(fixture.ApproverId,
            new GenerateSystemDescriptionExport(fixture.TenantId, fixture.ProgramId,
                descriptionId, 1, "pdf", "type-i-description-v1")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new Bdgrz.Compliance.Features.SystemDescription.SystemDescription(fixture.TenantId,
            fixture.ProgramId, descriptionId));

        // Assert
        Assert.True(review.IsSuccess, review.Error?.Message);
        Assert.True(approval.IsSuccess, approval.Error?.Message);
        Assert.True(binding.IsSuccess, binding.Error?.Message);
        Assert.True(export.IsSuccess, export.Error?.Message);
        Assert.Equal("approved", saved.CurrentVersion!.Status);
        Assert.Equal("type_i_baseline", Assert.Single(saved.Read().Bindings).Purpose);
        Assert.Equal("pdf", Assert.Single(saved.Read().Exports).Format);
    }

    [Fact]
    public async Task ShouldRejectAuthorApprovalGivenReviewedSystemDescription()
    {
        // Arrange
        await using var fixture = new Fixture();
        var descriptionId = await fixture.SeedDescriptionAsync(typeII: false);
        var review = await fixture.ReviewHandler().HandleAsync(Fixture.Context(fixture.ReviewerId,
            new ReviewSystemDescription(fixture.TenantId, fixture.ProgramId, descriptionId,
                1, "approve", "Independent review complete.")), CancellationToken.None);
        Assert.True(review.IsSuccess, review.Error?.Message);

        // Act
        var result = await fixture.ApproveHandler().HandleAsync(Fixture.Context(fixture.UserId,
            new ApproveSystemDescription(fixture.TenantId, fixture.ProgramId, descriptionId,
                1, "Author cannot approve their own description.")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new Bdgrz.Compliance.Features.SystemDescription.SystemDescription(fixture.TenantId,
            fixture.ProgramId, descriptionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(saved.Read().Approvals);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRecordNonMaterialPeriodChangeDecisionGivenCurrentTypeIIDraft()
    {
        // Arrange
        await using var fixture = new Fixture();
        var changeId = Uuid.CreateVersion4();
        var descriptionId = await fixture.SeedDescriptionAsync(typeII: true, changeId);

        // Act
        var result = await fixture.NoChangeHandler().HandleAsync(Fixture.Context(fixture.ReviewerId,
            new RecordSystemDescriptionNoChangeDecision(fixture.TenantId, fixture.ProgramId,
                descriptionId, 1, changeId, "The reviewed change does not alter the described system.")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new Bdgrz.Compliance.Features.SystemDescription.SystemDescription(fixture.TenantId,
            fixture.ProgramId, descriptionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(changeId, Assert.Single(saved.Read().NoChangeDecisions).ChangeId);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldProposeSuccessorFromApprovedVersionGivenChangeReason()
    {
        // Arrange
        await using var fixture = new Fixture();
        var descriptionId = await fixture.SeedApprovedDescriptionAsync();

        // Act
        var result = await fixture.ProposeSuccessorHandler().HandleAsync(Fixture.Context(
            fixture.UserId, new ProposeSystemDescriptionSuccessor(fixture.TenantId,
                fixture.ProgramId, descriptionId, 3, "Reflect the approved service expansion.")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new Bdgrz.Compliance.Features.SystemDescription.SystemDescription(
            fixture.TenantId, fixture.ProgramId, descriptionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("approved", saved.FindVersion(1)!.Status);
        Assert.Equal("draft", saved.CurrentVersion!.Status);
        Assert.Equal(2, saved.CurrentVersion.Version);
        Assert.Equal("Reflect the approved service expansion.", saved.CurrentVersion.ChangeReason);
        Assert.Equal("The Sample System", saved.CurrentVersion.Content.Title);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now.AddMinutes(3));
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid PeriodId { get; } = Uuid.CreateVersion4();
        public Uuid CloseSnapshotId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public ReviewSystemDescriptionHandler ReviewHandler() => new(Executor, _clock);
        public ApproveSystemDescriptionHandler ApproveHandler() => new(Executor, _clock);
        public RecordSystemDescriptionNoChangeDecisionHandler NoChangeHandler() => new(Executor, _clock);
        public BindSystemDescriptionToExaminationHandler BindHandler() => new(Executor, _clock);
        public GenerateSystemDescriptionExportHandler ExportHandler() => new(Executor, _clock);
        public ProposeSystemDescriptionSuccessorHandler ProposeSuccessorHandler() =>
            new(Executor, _clock);

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
            new HttpInvocation("POST", "/api/v1/system-descriptions",
                "/api/v1/system-descriptions", "system-description-test")));

        public async Task<Uuid> SeedDescriptionAsync(bool typeII, Uuid changeId = default)
        {
            var descriptionId = Uuid.CreateVersion4();
            var content = Content(changeId);
            var scope = typeII
                ? new SystemDescriptionScope("type_ii", ExaminationId, BaselineId, PeriodId,
                    CloseSnapshotId)
                : new SystemDescriptionScope("type_i", ExaminationId, BaselineId, null, null);
            var aggregate = new Bdgrz.Compliance.Features.SystemDescription.SystemDescription(
                TenantId, ProgramId, descriptionId);
            Assert.Null(aggregate.Create(Uuid.CreateVersion4(), scope, content,
                RbacIds.Member(TenantId, UserId), "author@example.test", Now));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
            return descriptionId;
        }

        public async Task<Uuid> SeedApprovedDescriptionAsync()
        {
            var descriptionId = Uuid.CreateVersion4();
            var aggregate = new Bdgrz.Compliance.Features.SystemDescription.SystemDescription(
                TenantId, ProgramId, descriptionId);
            Assert.Null(aggregate.Create(Uuid.CreateVersion4(), new SystemDescriptionScope("type_i",
                    ExaminationId, BaselineId, null, null), Content(Uuid.Empty),
                RbacIds.Member(TenantId, UserId), "author@example.test", Now));
            Assert.Null(aggregate.Review(1, RbacIds.Member(TenantId, ReviewerId),
                "reviewer@example.test", "approve", "Reviewed exact draft.", Now.AddMinutes(1)));
            Assert.Null(aggregate.Approve(1, RbacIds.Member(TenantId, ApproverId),
                "approver@example.test", "Management approved the exact version.",
                Now.AddMinutes(2)));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
            return descriptionId;
        }

        static SystemDescriptionDraftContent Content(Uuid changeId) =>
            new("The Sample System", [
                Section("services"),
                Section("commitments_requirements"),
                Section("components_infrastructure_software"),
                Section("people"),
                Section("procedures"),
                Section("information_data_flows"),
                Section("system_boundary"),
                Section("control_environment"),
                Section("user_entity_controls"),
                Section("subservice_organizations"),
                Section("exclusions"),
                Section("relevant_changes"),
            ], [], changeId == Uuid.Empty ? [] : [new SystemDescriptionPeriodChange(changeId,
                false, new string('a', 64))], []);

        static SystemDescriptionSection Section(string id) =>
            new(id, id.Replace('_', ' '), "Current system fact.", "complete", null);

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "system-description@example.test")], "BdgrzSession"));

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
