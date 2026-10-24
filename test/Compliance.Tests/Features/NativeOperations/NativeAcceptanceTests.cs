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
