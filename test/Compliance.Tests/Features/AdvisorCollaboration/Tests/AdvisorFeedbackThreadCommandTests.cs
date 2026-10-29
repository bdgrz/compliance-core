using System.Globalization;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

/// <summary>
///     The thread owner's commands: corrections preserve the original contribution, every command is bound to its own
///     identity and to the thread revision, and a thread keeps working after its handoff ended.
/// </summary>
public sealed class AdvisorFeedbackThreadCommandTests
{
    static CorrectAdvisorFeedback Correction(AdvisorHost host, Uuid handoffId, Uuid feedbackId, long expected = 1,
        Uuid? correctionId = null, string body = "Please identify the review owner.",
        string reason = "The original wording was incomplete.") => new(host.TenantId, host.ProgramId, handoffId,
        feedbackId, correctionId ?? Uuid.CreateVersion4(), body, reason, expected);

    [Fact]
    public async Task ShouldRecordLinkedCorrectionAndPreserveOriginalContributionGivenPersonalHttpActor()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var correctionId = Uuid.CreateVersion4();
        host.Clock.Advance(TimeSpan.FromMinutes(7));

        // Act
        var result = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            correctionId: correctionId)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Please clarify the control owner.", thread.Recorded!.Body);
        var correction = Assert.Single(thread.Corrections);
        Assert.Equal(correctionId, correction.CorrectionId);
        Assert.Equal("Please identify the review owner.", correction.CorrectedBody);
        Assert.Equal("The original wording was incomplete.", correction.CorrectionReason);
        Assert.Equal(RbacIds.Member(host.TenantId, host.UserId).ToString(), correction.CorrectedBy.Id);
        Assert.Equal(AdvisorHost.Start.AddMinutes(7), correction.CorrectedAt);
        Assert.Equal(2, thread.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactCorrectionRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var request = Correction(host, handoffId, feedbackId);
        Assert.True((await host.CorrectHandler.HandleAsync(host.Http(request), CancellationToken.None)).IsSuccess);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, "Updated.", 2)),
            CancellationToken.None)).IsSuccess);

        // Act
        var retry = await host.CorrectHandler.HandleAsync(host.Http(request), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(3, thread.Revision);
        Assert.Single(thread.Corrections);
    }

    [Fact]
    public async Task ShouldConflictGivenSameCorrectionIdentityWithChangedIntentOrOriginalRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var correctionId = Uuid.CreateVersion4();
        Assert.True((await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            correctionId: correctionId)), CancellationToken.None)).IsSuccess);

        // Act
        var otherBody = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            correctionId: correctionId, body: "Another wording.")), CancellationToken.None);
        var otherReason = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            correctionId: correctionId, reason: "Another reason.")), CancellationToken.None);
        var otherRevision = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            expected: 2, correctionId: correctionId)), CancellationToken.None);
        var otherActor = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            correctionId: correctionId), Uuid.CreateVersion4()), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.All(new[] { otherBody, otherReason, otherRevision, otherActor },
            result => Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind));
        Assert.Single(thread.Corrections);
        Assert.Equal(2, thread.Revision);
    }

    [Fact]
    public async Task ShouldRejectGivenStaleThreadRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();

        // Act
        var result = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            expected: 0)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(thread.Corrections);
        Assert.Equal(1, thread.Revision);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenMissingMalformedOrConflictingRouteIdentity()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();

        // Act
        var noCorrection = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            correctionId: Uuid.Empty)), CancellationToken.None);
        var noFeedback = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, Uuid.Empty)),
            CancellationToken.None);
        var blankBody = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            body: " ")), CancellationToken.None);
        var blankReason = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId,
            reason: " ")), CancellationToken.None);
        var noCommand = await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(Uuid.Empty,
            host.TenantId, host.ProgramId, handoffId, feedbackId, "Updated.", 1)), CancellationToken.None);
        var noResponse = await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, " ", 1)),
            CancellationToken.None);
        var negative = await host.ResolveHandler.HandleAsync(host.Http(new ResolveAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, -1)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.All(new[] { noCorrection, noFeedback, blankBody, blankReason, noCommand, noResponse, negative },
            result => Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind));
        Assert.Equal(1, thread.Revision);
    }

    [Fact]
    public async Task ShouldNotCorrectFeedbackGivenDifferentProgramScope()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var request = Correction(host, handoffId, feedbackId) with { ProgramId = Uuid.CreateVersion4() };

        // Act
        var result = await host.CorrectHandler.HandleAsync(host.Http(request), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(thread.Corrections);
        Assert.Equal(1, thread.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenFeedbackCorrection()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();

        // Act
        var result = await host.CorrectHandler.HandleAsync(AdvisorHost.System(Correction(host, handoffId, feedbackId)),
            CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(thread.Corrections);
    }

    [Fact]
    public async Task ShouldRetryGivenFeedbackAdmittedButNotMaterializedYetAndNotFoundGivenUnknownFeedback()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2))).IsSuccess);

        // Act
        var pending = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId, 0)),
            CancellationToken.None);
        var unknown = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId,
            Uuid.CreateVersion4(), 0)), CancellationToken.None);
        await host.MaterializeAsync(handoffId);
        var materialized = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, pending.Error?.Kind);
        Assert.True(pending.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.True(materialized.IsSuccess, materialized.Error?.Message);
    }

    [Fact]
    public async Task ShouldRespondAndResolveGivenHandoffRevokedAfterAdmission()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        Assert.True((await host.EndAsync(handoffId, 3, revoke: true)).IsSuccess);

        // Act
        var respond = await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, "We updated it.", 1)),
            CancellationToken.None);
        var resolve = await host.ResolveHandler.HandleAsync(host.Http(new ResolveAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, 2)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(respond.IsSuccess, respond.Error?.Message);
        Assert.True(resolve.IsSuccess, resolve.Error?.Message);
        Assert.Equal(3, thread.Revision);
        Assert.Equal("revoked", saved.Status);
        Assert.Equal(4, saved.Revision);
    }

    [Fact]
    public async Task ShouldRecordClientResponseAgainstExplicitHandoffGivenPersonalHttpActor()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        host.Clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId,
            "  We updated the control draft.  ", 1)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var response = Assert.Single(thread.Responses);
        Assert.Equal("We updated the control draft.", response.Response);
        Assert.Equal(RbacIds.Member(host.TenantId, host.UserId),
            Uuid.Parse(response.RespondedBy.Id, CultureInfo.InvariantCulture));
        Assert.Equal(AdvisorHost.Start.AddMinutes(5), response.RespondedAt);
        Assert.Equal(2, thread.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactResponseRetryAndConflictGivenChangedResponse()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var commandId = Uuid.CreateVersion4();
        RespondToAdvisorFeedback Respond(string text, long expected = 1) => new(commandId, host.TenantId,
            host.ProgramId, handoffId, feedbackId, text, expected);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(Respond("Done.")), CancellationToken.None)).IsSuccess);

        // Act
        var retry = await host.RespondHandler.HandleAsync(host.Http(Respond("Done.")), CancellationToken.None);
        var changed = await host.RespondHandler.HandleAsync(host.Http(Respond("Other.")), CancellationToken.None);
        var revision = await host.RespondHandler.HandleAsync(host.Http(Respond("Done.", 2)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, revision.Error?.Kind);
        Assert.Single(thread.Responses);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenAdvisorFeedbackResponse()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();

        // Act
        var result = await host.RespondHandler.HandleAsync(AdvisorHost.System(new RespondToAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, "Updated.", 1)),
            CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(thread.Responses);
    }

    [Fact]
    public async Task ShouldNotRespondGivenHandoffInDifferentProgramScope()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();

        // Act
        var result = await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, Uuid.CreateVersion4(), handoffId, feedbackId,
            "Response from the wrong program.", 1)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(thread.Responses);
    }

    [Fact]
    public async Task ShouldResolveFeedbackOnceAndListItResolvedGivenPersonalHttpActor()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var commandId = Uuid.CreateVersion4();
        var request = new ResolveAdvisorFeedback(commandId, host.TenantId, host.ProgramId, handoffId, feedbackId, 1);
        host.Clock.Advance(TimeSpan.FromMinutes(9));

        // Act
        var first = await host.ResolveHandler.HandleAsync(host.Http(request), CancellationToken.None);
        var retry = await host.ResolveHandler.HandleAsync(host.Http(request), CancellationToken.None);
        var second = await host.ResolveHandler.HandleAsync(host.Http(request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 2,
        }), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);
        var listed = await host.ListFeedbackHandler.HandleAsync(AdvisorHost.System(new ListAdvisorFeedback(host.TenantId,
            host.ProgramId, handoffId)), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Equal(AdvisorHost.Start.AddMinutes(9), thread.Resolved!.ResolvedAt);
        Assert.Equal(RbacIds.Member(host.TenantId, host.UserId).ToString(), thread.Resolved.ResolvedBy.Id);
        Assert.Equal(2, thread.Revision);
        Assert.True(listed.IsSuccess, listed.Error?.Message);
        Assert.Equal(thread.Resolved.ResolvedBy, Assert.Single(listed.Value.Items).ResolvedBy);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenAdvisorFeedbackResolution()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();

        // Act
        var result = await host.ResolveHandler.HandleAsync(AdvisorHost.System(new ResolveAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, 1)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null(thread.Resolved);
    }

    [Fact]
    public async Task ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome()
    {
        // Arrange
        // Create, ready, admit, then the correction is the fourth native append.
        await using var host = new AdvisorHost(wrapExecutor: inner => new AdvisorHost.CommitThenFailOnceExecutor(inner, 4));
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();

        // Act
        var result = await host.CorrectHandler.HandleAsync(host.Http(Correction(host, handoffId, feedbackId)),
            CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(thread.Corrections);
        Assert.Equal(2, thread.Revision);
    }
}
