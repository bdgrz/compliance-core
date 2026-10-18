using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditorRequests;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordAuditorRequestGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedMemberAsync(fixture.UserId);
        var request = fixture.NewRecordCommand();

        // Act
        var result = await fixture.RecordHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, request.RequestId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(request.Wording, saved.Current!.Wording);
        Assert.Equal(request.OwnerMemberId, saved.Current.OwnerMemberId);
        Assert.True(saved.Current.RequiresInternalReview);
    }

    [Fact]
    public async Task ShouldReviseRequestAtExpectedRevisionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.SeedRequestAsync();
        var command = new ReviseAuditorRequest(fixture.TenantId, fixture.ProgramId, id, 1,
            "Updated request wording", "Updated evidence scope", "high",
            new DateOnly(2026, 11, 1), "Clarified the request after auditor discussion");

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, id));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(command.Wording, saved.Current!.Wording);
        Assert.Equal(command.ChangeReason, saved.Current.TermsHistory[^1].ChangeReason);
    }

    [Fact]
    public async Task ShouldDiscardStaleRequestRevisionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.SeedRequestAsync();
        var command = new ReviseAuditorRequest(fixture.TenantId, fixture.ProgramId, id, 0,
            "Stale wording", "Stale scope", "normal", new DateOnly(2026, 11, 1), "Stale edit");

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, id));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
        Assert.Equal("External request wording", saved.Current!.Wording);
    }

    [Fact]
    public async Task ShouldAssignRequestAndRecordCommentGivenPersonalCommands()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.SeedRequestAsync();
        var assignedUser = Uuid.CreateVersion4();
        await fixture.SeedMemberAsync(assignedUser);
        var assignedOwner = RbacIds.Member(fixture.TenantId, assignedUser);
        var assignment = new AssignAuditorRequest(fixture.TenantId, fixture.ProgramId, id,
            assignedOwner, new DateOnly(2026, 11, 2), "Reassigned to the evidence owner");

        // Act
        var assigned = await fixture.AssignHandler().HandleAsync(fixture.Context(assignment),
            CancellationToken.None);
        var comment = await fixture.CommentHandler().HandleAsync(fixture.Context(
            new RecordAuditorRequestComment(fixture.TenantId, fixture.ProgramId, id,
                "The owner is collecting the requested support.", "internal")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, id));

        // Assert
        Assert.True(assigned.IsSuccess, assigned.Error?.Message);
        Assert.True(comment.IsSuccess, comment.Error?.Message);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(assignedOwner, saved.Current!.OwnerMemberId);
        Assert.Equal("internal", Assert.Single(saved.Current.Comments).Compartment);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenAuditorRequestCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = fixture.NewRecordCommand();
        var context = new Cntryl.Portia.RequestContext<RecordAuditorRequest>(command,
            new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/auditor-requests", "/auditor-requests", "test")));

        // Act
        var result = await fixture.RecordHandler().HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, command.RequestId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldKeepRequestOpenGivenMissingResolutionPrerequisitesWhenResolveAndReopenCommands()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.SeedRequestAsync();

        // Act
        var resolved = await fixture.ResolveHandler().HandleAsync(fixture.Context(
            new ResolveAuditorRequest(fixture.TenantId, fixture.ProgramId, id,
                "No response has been accepted yet.")), CancellationToken.None);
        var reopened = await fixture.ReopenHandler().HandleAsync(fixture.Context(
            new ReopenAuditorRequest(fixture.TenantId, fixture.ProgramId, id,
                "The request is still open.")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, id));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, resolved.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, reopened.Error?.Kind);
        Assert.Equal("open", saved.Current!.Status);
        Assert.Equal(1, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        readonly TimeProvider _clock = new FixedTimeProvider(Now);

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "attest@example.test")], "BdgrzSession"));

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

        public RecordAuditorRequest NewRecordCommand() => new(TenantId, ProgramId,
            Uuid.CreateVersion4(), ExaminationId, "external_email", "mail-001",
            "External request wording", "Evidence for the examination period", "normal",
            RbacIds.Member(TenantId, UserId), new DateOnly(2026, 10, 31));

        public RecordAuditorRequestHandler RecordHandler() => new(Executor, Reader, _clock);
        public ReviseAuditorRequestHandler ReviseHandler() => new(Executor, _clock);
        public AssignAuditorRequestHandler AssignHandler() => new(Executor, Reader, _clock);
        public RecordAuditorRequestCommentHandler CommentHandler() => new(Executor, _clock);
        public ResolveAuditorRequestHandler ResolveHandler() => new(Executor, _clock);
        public ReopenAuditorRequestHandler ReopenHandler() => new(Executor, _clock);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, new HttpInvocation("POST", "/api/v1/auditor-requests",
                "/api/v1/auditor-requests", "auditor-request-handler-test")));

        public async Task<Uuid> SeedRequestAsync()
        {
            await SeedMemberAsync(UserId);
            var requestId = Uuid.CreateVersion4();
            var aggregate = new AuditorRequest(TenantId, ProgramId, requestId);
            Assert.Null(aggregate.Create(new AuditorRequestContent(ExaminationId, "external_email",
                "mail-seed", "External request wording", "Evidence for the examination period",
                "normal", RbacIds.Member(TenantId, UserId), new DateOnly(2026, 10, 31), true),
                RbacIds.Member(TenantId, UserId), "attest@example.test", Now));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
            return requestId;
        }

        public async Task SeedMemberAsync(Uuid userId)
        {
            var member = new Member(TenantId, userId);
            Assert.True(member.Register().IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

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
