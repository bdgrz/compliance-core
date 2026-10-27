using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
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
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Commitments.Tests;

public sealed class CommitmentReviewTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly January = new(2027, 1, 1);

    static CommitmentRules Draft(string kind = "service_commitment")
    {
        var id = CommitmentRules.IdFor(TenantId, ProgramId, kind, "SC-01");
        var draft = new CommitmentRules(TenantId, id);
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId, kind, "SC-01",
            "Statement", "Context", "Contract 4.1", AuthorId, "Author", Now).IsSuccess);
        return draft;
    }

    static CommandFailure? Accept(CommitmentRules draft, long revision, Uuid reviewer,
        DateOnly effectiveFrom, SeparationOfDutiesWaiverProof? waiver = null,
        string interpretation = "supported")
    {
        var reviewId = Uuid.CreateVersion4();
        var review = draft.Review(ProgramId, revision, reviewId, "accept", "Security lead",
            "applicable", interpretation, "Read against MSA 4.1", "Verified with owner",
            reviewer, "Reviewer", Now.AddMinutes(5), waiver,
            draft.SourceReference, "Signed MSA v3, section 4.1");
        return review ?? draft.Approve(ProgramId, revision, Uuid.CreateVersion4(), reviewId,
            effectiveFrom, "Approved for the engagement", "digest", ApproverId, "Approver",
            Now.AddMinutes(6));
    }

    [Fact]
    public void ShouldCreateImmutableEffectiveVersionGivenIndependentAcceptedReview()
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = Accept(draft, 1, ReviewerId, January);

        // Assert
        Assert.Null(failure);
        Assert.Equal(1, draft.EffectiveVersionCount);
        Assert.Equal(1, draft.EffectiveVersionOn(January));
        Assert.Null(draft.EffectiveVersionOn(January.AddDays(-1)));
        var events = draft.PendingChanges;
        var reviewed = Assert.IsType<CommitmentReviewedContent>(events[^2]);
        Assert.Null(reviewed.Version);
        Assert.Equal("Security lead", reviewed.OwnerReference);
        Assert.Equal(ReviewerId.ToString(), reviewed.Actor.Id);
        var approved = Assert.IsType<CommitmentApprovedContent>(events[^1]);
        Assert.Equal(1, approved.Version);
        Assert.Equal(reviewed.DecisionId, approved.AcceptedReviewDecisionId);
    }

    [Fact]
    public void ShouldRejectReviewGivenReviewerAuthoredPendingRevision()
    {
        // Arrange
        var draft = Draft();
        Assert.Null(draft.Revise(ProgramId, 1, "Revised", "Context", "Contract 4.1",
            ReviewerId, "Reviewer", Now.AddMinutes(1)));

        // Act
        var selfReview = Accept(draft, 2, ReviewerId, January);
        var originalAuthor = Accept(draft, 2, AuthorId, January);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(selfReview).Code);
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(originalAuthor).Code);
        Assert.Equal(0, draft.EffectiveVersionCount);
    }

    [Fact]
    public void ShouldAllowAuthorReviewGivenApprovedExactWaiver()
    {
        // Arrange
        var draft = Draft();
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Commitment,
            draft.Id, draft.Id, 1, SeparationOfDutiesActions.Review);
        var wrongRevision = CreateWaiver(scope with { Revision = 2 }, AuthorId);
        var valid = CreateWaiver(scope, AuthorId);

        // Act
        var denied = Accept(draft, 1, AuthorId, January, wrongRevision);
        var allowed = Accept(draft, 1, AuthorId, January, valid);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(denied).Code);
        Assert.Null(allowed);
        Assert.Equal(valid.WaiverId, Assert.IsType<CommitmentReviewedContent>(
            draft.PendingChanges[^2])
            .SeparationOfDutiesWaiverId);
    }

    [Theory]
    [InlineData("", "applicable", "supported")]
    [InlineData("Owner", "maybe", "supported")]
    [InlineData("Owner", "applicable", "assumed")]
    public void ShouldRejectAcceptanceGivenUnverifiedOwnerApplicabilityOrInterpretation(
        string owner, string applicability, string interpretation)
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = draft.Review(ProgramId, 1, Uuid.CreateVersion4(), "accept", owner,
            applicability, interpretation, null, "Rationale", ReviewerId, "Reviewer", Now,
            sourceVerifiedReference: "Contract 4.1", sourceEvidence: "Signed MSA");

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
    }

    [Fact]
    public void ShouldKeepUnsupportedInterpretationVisibleGivenAcceptedReview()
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = Accept(draft, 1, ReviewerId, January, interpretation: "unsupported");

        // Assert
        Assert.Null(failure);
        Assert.Equal("unsupported", Assert.IsType<CommitmentReviewedContent>(
            draft.PendingChanges[^2]).Interpretation);
    }

    [Theory]
    [InlineData("service_commitment", "service_organization", true)]
    [InlineData("system_requirement", "service_organization", true)]
    [InlineData("user_entity_responsibility", "user_entity", false)]
    [InlineData("subservice_responsibility", "subservice_organization", false)]
    public void ShouldNeverTreatCarveOutResponsibilityAsInternalGivenKind(string kind,
        string performedBy, bool internallyPerformed)
    {
        // Arrange
        var normalized = CommitmentRules.NormalizeKind(kind);

        // Act
        var party = CommitmentRules.PerformedBy(normalized);

        // Assert
        Assert.Equal(performedBy, party);
        Assert.Equal(internallyPerformed, CommitmentRules.IsInternallyPerformed(normalized));
    }

    [Fact]
    public void ShouldRequireLaterEffectiveDateAndNewRevisionGivenSuccessorVersion()
    {
        // Arrange
        var draft = Draft();
        Assert.Null(Accept(draft, 1, ReviewerId, January));

        // Act
        var sameRevision = Accept(draft, 1, ReviewerId, January.AddDays(1));
        Assert.Null(draft.Revise(ProgramId, 1, "Successor", "Context", "Contract 4.2",
            AuthorId, "Author", Now.AddMinutes(6)));
        var notLater = Accept(draft, 2, ReviewerId, January);
        var successor = Accept(draft, 2, ReviewerId, January.AddMonths(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(sameRevision).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(notLater).Code);
        Assert.Null(successor);
        Assert.Equal(2, draft.EffectiveVersionCount);
        Assert.Equal(1, draft.EffectiveVersionOn(January.AddDays(10)));
        Assert.Equal(2, draft.EffectiveVersionOn(January.AddMonths(2)));
    }

    [Fact]
    public void ShouldRecordRequestChangesWithoutVersionGivenRationale()
    {
        // Arrange
        var draft = Draft();

        // Act
        var missingRationale = draft.Review(ProgramId, 1, Uuid.CreateVersion4(),
            "request_changes", null, null, null, null, " ", ReviewerId, "Reviewer", Now);
        var failure = draft.Review(ProgramId, 1, Uuid.CreateVersion4(), "request_changes",
            null, null, null, null, "Owner unclear", ReviewerId, "Reviewer", Now);
        var stale = draft.Review(ProgramId, 3, Uuid.CreateVersion4(), "request_changes",
            null, null, null, null, "Owner unclear", ReviewerId, "Reviewer", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingRationale).Code);
        Assert.Null(failure);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(0, draft.EffectiveVersionCount);
        Assert.Null(Assert.IsType<CommitmentReviewedContent>(
            draft.PendingChanges[^1]).Version);
    }

    static SeparationOfDutiesWaiverProof CreateWaiver(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiary)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, beneficiary, Uuid.CreateVersion4(), "Requester",
            "No other qualified reviewer is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return ControlTestWaivers.Proof(waiver);
    }
}
