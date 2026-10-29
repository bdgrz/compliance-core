using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

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
