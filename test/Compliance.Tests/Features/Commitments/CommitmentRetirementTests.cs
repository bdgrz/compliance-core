using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentRetirementTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetireEffectiveCommitmentAndRejectFurtherChangesGivenRetirement()
    {
        // Arrange
        var draft = EffectiveDraft();
        var effectiveOn = new DateOnly(2027, 2, 1);
        var retiredAt = Now.AddMinutes(15);

        // Act
        var retired = draft.Retire(ProgramId, draft.Revision, effectiveOn,
            "The service commitment was superseded by the current engagement terms.",
            ApproverId, "Approver", retiredAt);
        var exactRetry = draft.Retire(ProgramId, draft.Revision, effectiveOn,
            "The service commitment was superseded by the current engagement terms.",
            ApproverId, "Approver", retiredAt);
        var changedRetry = draft.Retire(ProgramId, draft.Revision, effectiveOn,
            "Different retirement reason", ApproverId, "Approver", retiredAt);
        var revise = draft.Revise(ProgramId, draft.Revision, "Changed statement", "Context",
            "MSA 4.1", AuthorId, "Author", retiredAt.AddMinutes(1));
        var events = draft.PendingChanges.ToArray();
        var replay = NewDraft();
        foreach (var change in events)
            replay.Apply(change);

        // Assert
        Assert.Null(retired);
        Assert.Null(exactRetry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changedRetry).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(revise).Code);
        Assert.True(draft.IsRetired);
        Assert.Equal(effectiveOn, draft.RetiredEffectiveOn);
        Assert.Equal("The service commitment was superseded by the current engagement terms.",
            draft.RetirementRationale);
        Assert.True(replay.IsRetired);
        Assert.Equal(effectiveOn, replay.RetiredEffectiveOn);
        Assert.IsType<CommitmentRetiredContent>(events[^1]);
    }

    [Fact]
    public void ShouldRejectRetirementBeforeAnEffectiveVersionGivenDraftOnly()
    {
        // Arrange
        var draft = NewDraft();
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId, "service_commitment",
            "SC-01", "Statement", "Context", "MSA 4.1", AuthorId, "Author", Now).IsSuccess);

        // Act
        var failure = draft.Retire(ProgramId, draft.Revision, new DateOnly(2027, 1, 1),
            "No approved version exists.", ApproverId, "Approver", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.False(draft.IsRetired);
        Assert.Single(draft.PendingChanges);
    }

    static CommitmentRules EffectiveDraft()
    {
        var draft = NewDraft();
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId, "service_commitment",
            "SC-01", "Statement", "Context", "MSA 4.1", AuthorId, "Author", Now).IsSuccess);
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(draft.Review(ProgramId, draft.Revision, reviewId, "accept", "Owner",
            "applicable", "supported", null, "Verified", ReviewerId, "Reviewer", Now.AddMinutes(5),
            sourceVerifiedReference: "MSA 4.1", sourceEvidence: "Signed agreement checked."));
        Assert.Null(draft.Approve(ProgramId, draft.Revision, Uuid.CreateVersion4(), reviewId,
            new DateOnly(2027, 1, 1), "Approved", "impact-digest", ApproverId, "Approver",
            Now.AddMinutes(10)));
        return draft;
    }

    static CommitmentRules NewDraft() => new(TenantId,
        CommitmentRules.IdFor(TenantId, ProgramId, "service_commitment", "SC-01"));
}
