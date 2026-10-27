using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

public sealed class NativeAcceptanceTests
{
    static readonly Uuid Logical = Uuid.Parse("00000000-0000-4000-8000-0000000000c1", CultureInfo.InvariantCulture);
    static readonly Uuid Other = Uuid.Parse("00000000-0000-4000-8000-0000000000c2", CultureInfo.InvariantCulture);
    static readonly EventStreamAddress Owner = new("tenant-a", "probes", "p1");
    static readonly ActorReference Actor = new("member", "m1", "Member One");
    static readonly ImmutableContentRef Response = new(Uuid.CreateVersion4(), new string('a', 64), 10,
        "application/json", FirstResponses.Schema);

    static OperationProvenance Provenance(Uuid logicalId, string kind = "Admit") => new(logicalId, logicalId, null,
        kind, Actor, Owner, "initial", 0, new string('d', 64), new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero),
        Owner, Owner);

    [Fact]
    public void ShouldKeepTwoCommandKindsApartGivenOneLogicalIdAndASelectedKind()
    {
        // Arrange
        var start = new NativeAcceptance(Logical, "Start");
        var complete = new NativeAcceptance(Logical, "Complete");
        var facts = new[]
        {
            new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical, "Start"), Response),
            new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical, "Complete"), Response),
        };

        // Act
        foreach (var fact in facts)
        {
            start.Observe(fact);
            complete.Observe(fact);
        }

        // Assert
        Assert.Same(facts[0], start.SelectedFact);
        Assert.Same(facts[1], complete.SelectedFact);
        Assert.Equal(NativeAcceptanceDecision.Replayed, start.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Start"))));
        Assert.Equal(NativeAcceptanceDecision.Replayed, complete.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Complete"))));
    }

    [Fact]
    public void ShouldDecideFromProvenanceGivenAFactThatIsNotNative()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        var fact = new ProbeEdited(Uuid.CreateVersion4(), "edited");

        // Act
        acceptance.Observe(fact, Provenance(Logical));
        var same = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical)));
        var changed = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Other")));

        // Assert
        Assert.Same(fact, acceptance.SelectedFact);
        Assert.Equal(NativeAcceptanceDecision.Replayed, same);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
    }

    [Fact]
    public void ShouldReplayGivenAcceptedFactWithEqualBinding()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical), Response));

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical)));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
    }

    [Fact]
    public void ShouldReplayOriginalFactGivenOnlyActorDisplayChanged()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        var original = Provenance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", original, Response));
        var candidate = original with { Actor = Actor with { Display = "Updated profile label" } };

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(candidate));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Equal(Actor, acceptance.SelectedProvenance!.Actor);
    }

    [Theory]
    [InlineData("member", "other-member")]
    [InlineData("platform_operator", "m1")]
    public void ShouldConflictGivenActorIdentityChanged(string kind, string id)
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        var original = Provenance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", original, Response));
        var candidate = original with { Actor = Actor with { Kind = kind, Id = id } };

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(candidate));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    [Fact]
    public void ShouldConflictGivenAcceptedFactWithChangedKind()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical), Response));

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Revise")));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    [Fact]
    public void ShouldIgnoreFactsGivenOtherLogicalIds()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);

        // Act
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Other), Response));

        // Assert
        Assert.Null(acceptance.SelectedFact);
        Assert.Equal(NativeAcceptanceDecision.None, acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical))));
    }

    [Fact]
    public void ShouldThrowGivenSecondAcceptedFactForSelectedLogicalId()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical), Response));

        // Act
        var failure = Record.Exception(() =>
            acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "q", Provenance(Logical), Response)));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }
}
