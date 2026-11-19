using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class ControlOccurrenceLifecycleRulesTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid Recorder = Uuid.CreateVersion4();

    [Theory]
    [InlineData("submitted")]
    [InlineData("approved")]
    [InlineData("accepted")]
    [InlineData("action_requested")]
    [InlineData("cancelled")]
    [InlineData("deferred")]
    public void ShouldRefuseCorrectionGivenAttestationWasNotReturned(string state)
    {
        // Arrange
        var current = Occurrence(state);

        // Act
        var result = ControlOperationRules.CheckCorrection(current, 2, Input(), "Correct record", [], Now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RequestErrorKind.Conflict, result.ToRequestError().Kind);
    }

    [Fact]
    public void ShouldAllowCorrectionGivenReturnedAttestation()
    {
        // Arrange
        var current = Occurrence("returned");

        // Act
        var result = ControlOperationRules.CheckCorrection(current, 2, Input(), "Correct record", [], Now);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("none", null)]
    [InlineData("none", " ")]
    [InlineData("unknown", "A reason")]
    public void ShouldRefuseAcceptedFailureGivenMissingFindingDecision(string? decision, string? rationale)
    {
        // Arrange
        var current = Occurrence("submitted");

        // Act
        var result = ControlOperationRules.CheckReview(current, current.TenantId, 2,
            current.Attestations[0].AttestationId, "accepted", "Reviewed failure", [],
            Uuid.CreateVersion4(), Now, null, decision, rationale);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RequestErrorKind.Validation, result.ToRequestError().Kind);
    }

    [Theory]
    [InlineData("raise", null)]
    [InlineData("none", "Existing remediation covers this occurrence")]
    public void ShouldAcceptFailureGivenExplicitFindingDecision(string decision, string? rationale)
    {
        // Arrange
        var current = Occurrence("submitted");

        // Act
        var result = ControlOperationRules.CheckReview(current, current.TenantId, 2,
            current.Attestations[0].AttestationId, "accepted", "Reviewed failure", [],
            Uuid.CreateVersion4(), Now, null, decision, rationale);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ShouldPresentOverdueGivenWindowEndedBeforeDueDate()
    {
        // Arrange
        var current = Occurrence("open") with { Attestations = [], DueOn = new DateOnly(2026, 11, 30) };

        // Act
        var presented = ControlOccurrenceFold.Present(current, new DateOnly(2026, 11, 4));

        // Assert
        Assert.Equal("missed", presented.State);
        Assert.Equal("open", current.State);
    }

    static AttestationInput Input() => new("failed", Now.AddMinutes(-1), null, null, null,
        "Control was not performed", [], new OperatingHolder("member", Recorder));

    static ControlOccurrenceView Occurrence(string state)
    {
        var actor = ActorReference.ForMember(Recorder, "Recorder");
        var attestation = new ControlAttestationView(Uuid.CreateVersion4(), 1, "failed", Now.AddHours(-1),
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, "Control failed", [],
            new OperatingHolder("member", Recorder), Recorder, actor, Now.AddHours(-1),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), []);
        return new ControlOccurrenceView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 2, "recurring", state, attestation.ControlVersionId, attestation.PlanVersionId,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 15), null,
            new OperatingHolder("member", Recorder), [attestation], [], []);
    }
}
