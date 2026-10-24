using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestCommandHandlerTests
{
    [Fact]
    public async Task ShouldRecordAuditorRequestGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        await fixture.SeedMemberAsync(fixture.UserId);
        var request = fixture.NewRecordCommand();

        // Act
        var result = await fixture.Handler<RecordAuditorRequestHandler>().HandleAsync(fixture.Context(request), CancellationToken.None);
        var saved = await fixture.RequestAsync(request.RequestId);
        var terms = await fixture.TermsAsync(request.RequestId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(request.Wording, terms.Wording);
        Assert.Equal(request.OwnerMemberId, terms.OwnerMemberId);
        Assert.True(terms.RequiresInternalReview);
    }

    [Fact]
    public async Task ShouldReviseRequestAtExpectedRevisionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        var command = new ReviseAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id, 1,
            "Updated request wording", "Updated evidence scope", "high", new DateOnly(2026, 11, 1),
            "Clarified the request after auditor discussion");

        // Act
        var result = await fixture.Handler<ReviseAuditorRequestHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);
        var saved = await fixture.RequestAsync(id);
        var terms = await fixture.TermsAsync(id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(command.Wording, terms.Wording);
        Assert.Equal(command.ChangeReason, terms.Reason);
    }

    [Fact]
    public async Task ShouldDiscardStaleRequestRevisionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        var command = new ReviseAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id, 0,
            "Stale wording", "Stale scope", "normal", new DateOnly(2026, 11, 1), "Stale edit");

        // Act
        var result = await fixture.Handler<ReviseAuditorRequestHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, (await fixture.RequestAsync(id)).Revision);
        Assert.Equal("External request wording", (await fixture.TermsAsync(id)).Wording);
    }

    [Fact]
    public async Task ShouldAssignRequestAndRecordCommentGivenPersonalCommands()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        var assignedUser = Uuid.CreateVersion4();
        await fixture.SeedMemberAsync(assignedUser);
        var owner = RbacIds.Member(fixture.TenantId, assignedUser);
        var assignment = new AssignAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            owner, new DateOnly(2026, 11, 2), "Reassigned to the evidence owner", 1);
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            "The owner is collecting the requested support.", "internal", 2);

        // Act
        var assigned = await fixture.Handler<AssignAuditorRequestHandler>().HandleAsync(fixture.Context(assignment), CancellationToken.None);
        var admitted = await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment), CancellationToken.None);
        var materialized = await fixture.MaterializeAsync(comment.CommentId);
        var thread = await fixture.Reader.HydrateAsync(new AuditorFeedbackThread(fixture.TenantId, fixture.ProgramId, id, comment.CommentId));
        var intent = await fixture.ReadAsync<ChildAdmissionIntent>(thread.Recorded!.Change);
        var authored = await fixture.ReadAsync<AuditorCommentContent>(intent.CanonicalAuthoredContent);

        // Assert
        Assert.True(assigned.IsSuccess, assigned.Error?.Message);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, materialized.State);
        Assert.Equal(3, (await fixture.RequestAsync(id)).Revision);
        Assert.Equal(owner, (await fixture.TermsAsync(id)).OwnerMemberId);
        Assert.Equal("internal", authored.Compartment);
        Assert.Equal(comment.Body, authored.Body);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenAuditorRequestCommand()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var command = fixture.NewRecordCommand();
        var context = new Cntryl.Portia.RequestContext<RecordAuditorRequest>(command, new RequestDispatchContext(RequestActor.System,
            new HttpInvocation("POST", "/auditor-requests", "/auditor-requests", "test")));

        // Act
        var result = await fixture.Handler<RecordAuditorRequestHandler>().HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, (await fixture.RequestAsync(command.RequestId)).Revision);
    }

    [Fact]
    public async Task ShouldKeepRequestOpenGivenMissingResolutionPrerequisitesWhenResolveAndReopenCommands()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();

        // Act
        var (resolved, _) = await fixture.ResolveAsync(id);
        var reopened = await fixture.Handler<ReopenAuditorRequestHandler>().HandleAsync(fixture.Context(
            new ReopenAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
                "The request is still open.", 1)), CancellationToken.None);
        var saved = await fixture.RequestAsync(id);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, resolved.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, reopened.Error?.Kind);
        Assert.True(saved.IsOpen);
        Assert.Equal(1, saved.Revision);
    }
    [Fact]
    public async Task ShouldRejectForeignProgramGivenKnownRequestIdentity()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        var otherProgram = Uuid.CreateVersion4();
        var revision = new ReviseAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, otherProgram, id, 0,
            "Cross-program change", "Cross-program scope", "normal", new DateOnly(2026, 11, 1), "Wrong route");
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, otherProgram, id,
            "Cross-program comment", "internal", 0);
        var reopen = new ReopenAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, otherProgram, id, "Wrong route", 0);
        var resolve = new ResolveAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, otherProgram, id,
            "Wrong route", 0, []);

        // Act
        var revised = await fixture.Handler<ReviseAuditorRequestHandler>().HandleAsync(fixture.Context(revision), CancellationToken.None);
        var commented = await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment), CancellationToken.None);
        var reopened = await fixture.Handler<ReopenAuditorRequestHandler>().HandleAsync(fixture.Context(reopen), CancellationToken.None);
        var resolved = await fixture.Handler<ResolveAuditorRequestHandler>().HandleAsync(fixture.Context(resolve), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, revised.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, commented.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, reopened.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, resolved.Error?.Kind);
        Assert.Equal(1, (await fixture.RequestAsync(id)).Revision);
        Assert.Equal(fixture.ProgramId, (await fixture.TermsAsync(id)).ProgramId);
    }

}
