using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestUnselectedHistoryTests
{
    [Fact]
    public async Task ShouldRejectDuplicateUnselectedCommentGivenDefaultRequestHydration()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var record = await SeedDuplicateAsync(fixture);

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await fixture.Reader.HydrateAsync(
            new AuditorRequest(fixture.TenantId, fixture.ProgramId, record.RequestId, record.RequestId)));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.IsNotType<ImmutableContentUnavailableException>(error);
    }

    [Fact]
    public async Task ShouldRejectExactEarlierRecordRetryGivenDuplicatedUnselectedComment()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var record = await SeedDuplicateAsync(fixture);
        Result result = default;

        // Act
        var error = await Record.ExceptionAsync(async () => result = await fixture.Handler<RecordAuditorRequestHandler>()
            .HandleAsync(fixture.Context(record), CancellationToken.None));

        // Assert
        Assert.False(error is null && result.IsSuccess,
            "An earlier accepted root must not bypass whole-prefix duplicate admission validation.");
        if (error is not null)
            Assert.IsType<ImmutableContentIntegrityException>(error);
        else
        {
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.False(result.Error!.IsTransient);
        }
    }

    static async Task<RecordAuditorRequest> SeedDuplicateAsync(AuditorRequestFixture fixture)
    {
        await fixture.SeedMemberAsync(fixture.UserId);
        var record = fixture.NewRecordCommand();
        var recorded = await fixture.Handler<RecordAuditorRequestHandler>().HandleAsync(fixture.Context(record),
            CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            record.RequestId, "A fully frozen comment admission.", "internal", 1);
        var first = await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment),
            CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var owner = await fixture.RequestAsync(record.RequestId);
        AuditorCommentAdmitted? prior = null;
        await foreach (var item in fixture.Events.ReadAsync(owner.Stream, 0, CancellationToken.None))
            if (item.Event is AuditorCommentAdmitted admitted)
                prior = admitted;
        Assert.NotNull(prior);
        var duplicateCommand = comment with { ExpectedRequestRevision = owner.Revision };
        var operation = prior.Operation with
        {
            OriginalExpectedRevision = (ulong)owner.Revision,
            AuthoredIntentSha256 = AuditorCommands.Digest(duplicateCommand),
        };
        var original = await fixture.ReadAsync<ChildAdmissionIntent>(prior.Intent);
        var authored = await AuditorContent.WriteAsync(fixture.Content, fixture.TenantId, AuditorContent.CommentSchema,
            new AuditorCommentContent(fixture.ProgramId, record.RequestId, comment.CommentId, comment.Body,
                comment.Compartment, fixture.MemberId, operation.Actor.Display, operation.AcceptedAt), CancellationToken.None);
        var intent = await AuditorContent.WriteAsync(fixture.Content, fixture.TenantId, ChildAdmissionIntent.Schema,
            new ChildAdmissionIntent(AuditorContent.Header(operation, operation.AcceptedAt), record.RequestId,
                (ulong)owner.Generation, AdmittedChildKind.AuditorComment, comment.CommentId, authored,
                original.SelectedSourceProofs, (ulong)owner.Revision + 1), CancellationToken.None);
        var response = await AuditorContent.AcceptedResponseAsync(fixture.Content, fixture.TenantId, comment.CommentId,
            operation.AcceptedAt, CancellationToken.None);
        var frozen = await FirstResponses.FreezeAsync(fixture.Content, fixture.TenantId.ToString(), response,
            CancellationToken.None);
        var duplicate = new AuditorCommentAdmitted(owner.Stream, owner.Revision + 1, owner.Generation, intent, null,
            operation, frozen, null);
        duplicate.AttachMetadata(prior.Metadata with { EventId = Uuid.CreateVersion4(), AggregateVersion = owner.Version + 1 });
        await fixture.Events.AppendAsync(owner.Stream, owner.CommittedStreamPosition, [duplicate], CancellationToken.None);
        return record;
    }
}
