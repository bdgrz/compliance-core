using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Governance.Commands;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;

namespace Bdgrz.Compliance.Tests.Features.Commitments.Governance;

public sealed class CommitmentGovernanceTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly ActorReference Other = ActorReference.ForMember(Uuid.CreateVersion4(), "Other");
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 11, 4);

    static CommitmentTerms Terms() => new("Service availability", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), ["security"], new("sla", "Service agreement", "2026-01", "2.1"));

    [Fact]
    public void ShouldApproveInOneStepAndRefuseAnyDraftAuthorGivenRevisionsBySeveralPeople()
    {
        // Arrange
        var terms = Terms();
        var owner = new Commitment(Tenant, Commitment.IdFor(Tenant, Program, "service_commitment", "SC-1"));
        Assert.Null(owner.Draft(new DraftCommitment(owner.Id, Tenant, Program, "service_commitment", "SC-1", terms), Author, Now));
        Assert.Null(owner.Revise(new ReviseCommitmentDraft(Uuid.CreateVersion4(), Tenant, Program, owner.Id, 1,
            terms with { Statement = "Availability commitment" }), Other, Now.AddMinutes(1)));
        var before = new AggregateScenario<Commitment>(owner).PendingEvents.ToArray();
        var review = new ReviewCommitmentDraft(Uuid.CreateVersion4(), Tenant, Program, owner.Id, 2,
            "approve", "Agreed", Today);

        // Act
        var self = owner.Review(review, Author, Uuid.CreateVersion4(), null, Now.AddMinutes(2));
        var accepted = owner.Review(review, ActorReference.ForMember(Uuid.CreateVersion4(), "Management"),
            Uuid.CreateVersion4(), null, Now.AddMinutes(2));

        // Assert
        Assert.NotNull(self);
        Assert.Null(accepted);
        Assert.Null(owner.OpenDraft);
        Assert.Single(owner.Versions);
        Assert.Equal(1, owner.Versions[0].Version);
        Assert.Equal(before.Length + 1, new AggregateScenario<Commitment>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldKeepApprovedVersionGivenChangedDraftDiscardedAndRetirementFinal()
    {
        // Arrange
        var terms = Terms();
        var owner = new Commitment(Tenant, Commitment.IdFor(Tenant, Program, "service_commitment", "SC-2"));
        var draft = new DraftCommitment(owner.Id, Tenant, Program, "service_commitment", "SC-2", terms);
        Assert.Null(owner.Draft(draft, Author, Now));
        Assert.Null(owner.Review(new(Uuid.CreateVersion4(), Tenant, Program, owner.Id, 1, "approve", "Agreed", Today),
            Other, Uuid.CreateVersion4(), null, Now));
        var approved = owner.Versions.Single();
        Assert.Null(owner.Propose(new(Uuid.CreateVersion4(), Tenant, Program, owner.Id, 2,
            terms with { Statement = "Changed" }), Author, Now));

        // Act
        var discard = owner.Discard(new(Uuid.CreateVersion4(), Tenant, Program, owner.Id, 3), Author, Now);
        var retirement = owner.Retire(new(Uuid.CreateVersion4(), Tenant, Program, owner.Id, 4, Today.AddDays(1),
            "Service ended"), Other, Now);
        var recreate = owner.Draft(draft with { Terms = terms with { Statement = "New promise" } }, Author, Now);

        // Assert
        Assert.Null(discard);
        Assert.Null(retirement);
        Assert.NotNull(recreate);
        Assert.Equal(approved, owner.Versions.Single());
        Assert.True(owner.IsActiveOn(Today));
        Assert.False(owner.IsActiveOn(Today.AddDays(1)));
        Assert.Null(owner.OpenDraft);
    }

    [Fact]
    public void ShouldReplayExactIntentWithoutFactAndRefuseChangedIntentGivenAcceptedDraft()
    {
        // Arrange
        var owner = new Commitment(Tenant, Commitment.IdFor(Tenant, Program, "system_requirement", "SR-1"));
        var request = new DraftCommitment(owner.Id, Tenant, Program, "system_requirement", "SR-1", Terms());
        Assert.Null(owner.Draft(request, Author, Now));
        var facts = new AggregateScenario<Commitment>(owner).PendingEvents.ToArray();

        // Act
        var same = owner.Draft(request, Author, Now.AddDays(2));
        var changed = owner.Draft(request with { Terms = request.Terms with { Statement = "Changed" } }, Author, Now);

        // Assert
        Assert.Null(same);
        Assert.NotNull(changed);
        Assert.Equal(facts, new AggregateScenario<Commitment>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldConfirmNewerVersionsAndRelinkPairGivenUnlinkedHistory()
    {
        // Arrange
        var commitment = Uuid.CreateVersion4();
        var control = Uuid.CreateVersion4();
        var owner = new CommitmentControlLink(Tenant, commitment, control);
        Assert.Null(owner.Link(new(Uuid.CreateVersion4(), Tenant, Program, commitment, control, 0, "Addresses promise"),
            1, 2, Author, Now));
        var unchanged = new ConfirmCommitmentControlLink(Uuid.CreateVersion4(), Tenant, Program, commitment, control,
            1, "Still addresses promise");

        // Act
        var noNewVersion = owner.Confirm(unchanged, 1, 2, Author, Now);
        var confirmation = owner.Confirm(unchanged, 2, 2, Author, Now);
        var unlink = owner.Unlink(new(Uuid.CreateVersion4(), Tenant, Program, commitment, control, 2,
            "Replaced control"), Author, Now);
        var relink = owner.Link(new(Uuid.CreateVersion4(), Tenant, Program, commitment, control, 3,
            "New scope"), 2, 3, Author, Now);

        // Assert
        Assert.NotNull(noNewVersion);
        Assert.Null(confirmation);
        Assert.Null(unlink);
        Assert.Null(relink);
        Assert.True(owner.IsLinked);
        Assert.Equal(2, owner.CommitmentVersion);
        Assert.Equal(3, owner.ControlVersion);
        Assert.Equal(4, owner.Revision);
    }
}
