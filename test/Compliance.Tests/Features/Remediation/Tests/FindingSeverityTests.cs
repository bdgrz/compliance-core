using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingSeverityTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");

    [Theory]
    [InlineData("low", true)]
    [InlineData("moderate", true)]
    [InlineData("high", true)]
    [InlineData("medium", false)]
    [InlineData("critical", false)]
    [InlineData("unknown", false)]
    [InlineData("Moderate", false)]
    public void ShouldApplyApprovedVocabularyWithoutChangingSourceGivenRaiseSeverity(string severity, bool allowed)
    {
        // Arrange
        var owner = NewFinding();
        var terms = Terms(severity);

        // Act
        var result = owner.Raise(Operation(owner, owner.FindingId, nameof(RaiseFinding), null), terms,
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), null);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? 1 : 0, new AggregateScenario<FindingAggregate>(owner).PendingEvents.Count);
        if (allowed)
        {
            Assert.Equal(severity, owner.Severity);
            Assert.Equal(terms.Source.SourceText, owner.Raised!.Source.SourceText);
        }
        else
            Assert.False(owner.IsRecorded);
    }

    [Theory]
    [InlineData("low", true)]
    [InlineData("moderate", true)]
    [InlineData("high", true)]
    [InlineData("medium", false)]
    [InlineData("critical", false)]
    [InlineData("unknown", false)]
    [InlineData("Moderate", false)]
    public void ShouldApplyApprovedVocabularyWithoutFactOnRefusalGivenRevisionSeverity(string severity, bool allowed)
    {
        // Arrange
        var owner = NewFinding();
        Assert.True(owner.Raise(Operation(owner, owner.FindingId, nameof(RaiseFinding), null), Terms("high"),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), null).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();
        var sourceText = owner.Raised!.Source.SourceText;
        var revision = new FindingRevision(severity, Uuid.CreateVersion4(), new DateOnly(2026, 12, 1),
            "VPN and SSO", "Leaver process", "Reviewed scope.", Actor, NativeFacts.Now);

        // Act
        var result = owner.Revise(Operation(owner, Uuid.CreateVersion4(), nameof(ReviseFinding), 1), revision,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? 1 : 0, new AggregateScenario<FindingAggregate>(owner).PendingEvents.Count);
        Assert.Equal(allowed ? 2 : 1, owner.Revision);
        Assert.Equal(sourceText, owner.Raised.Source.SourceText);
    }

    [Theory]
    [InlineData("low", true)]
    [InlineData("moderate", true)]
    [InlineData("high", true)]
    [InlineData("medium", false)]
    [InlineData("critical", false)]
    public async Task ShouldEnforceApprovedSeverityAtHandlerGivenRaisedFinding(string severity, bool allowed)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var request = fixture.Raise(fixture.OwnerMemberId) with { Severity = severity };
        var scenario = fixture.Scenario(fixture.LeadUserId).When(request);

        // Act
        if (allowed)
            await scenario.ExpectSuccess();
        else
            await scenario.ExpectFailure(RequestErrorKind.Validation);
        var owner = await fixture.FindingStreamAsync(request.FindingId);

        // Assert
        Assert.Equal(allowed, owner.IsRecorded);
        if (allowed)
        {
            Assert.Equal(severity, owner.Severity);
            Assert.Equal(request.Source.SourceText, owner.Raised!.Source.SourceText);
        }
    }

    [Theory]
    [InlineData("medium")]
    [InlineData("critical")]
    public void ShouldRejectUnsupportedSeverityGivenFindingFactReplay(string severity)
    {
        // Arrange
        var owner = NewFinding();
        Assert.True(owner.Raise(Operation(owner, owner.FindingId, nameof(RaiseFinding), null), Terms("high"),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), null).IsSuccess);
        var fact = Assert.IsType<FindingRaisedV2>(Assert.Single(new AggregateScenario<FindingAggregate>(owner).PendingEvents));
        var changed = fact with { Severity = severity };
        var replay = new AggregateScenario<FindingAggregate>(
            new FindingAggregate(owner.TenantId, owner.ProgramId, owner.FindingId));

        // Act
        var failure = Record.Exception(() => replay.Given(changed));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ShouldRaiseModerateFindingAndPreserveWorkPriorityGivenFailedOccurrenceReaction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var failed = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed, "failed", [], "Export tool was down."));
        var attestation = failed.Attestations[0].AttestationId;
        var request = new RaiseOccurrenceFinding(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion5(attestation, "finding"), fixture.ControlId, failed.OccurrenceId,
            attestation.ToString(), "control_occurrence", "failed: Export tool was down.");

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(request).ExpectSuccess();
        var finding = await fixture.GetFindingAsync(request.FindingId);

        // Assert
        Assert.Equal("moderate", finding.Severity);
        Assert.Equal("medium", FindingStates.Materiality(finding.Severity));
        Assert.Equal(request.SourceText, finding.Source.SourceText);
    }

    static FindingAggregate NewFinding() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static FindingRaiseTerms Terms(string severity) => new(new FindingSource("manual", null, null,
        "  Original source wording.  "), "Retained access", "Leaver access was retained.", severity, "VPN",
        Uuid.CreateVersion4(), new DateOnly(2026, 12, 1), Actor, NativeFacts.Now, "manual", null);

    static Bdgrz.Compliance.Features.NativeOperations.Models.OperationProvenance Operation(
        FindingAggregate owner, Uuid logicalId, string kind, ulong? expected) =>
        FindingContent.Provenance(logicalId, kind, owner.Stream, Actor, expected, new string('a', 64), NativeFacts.Now) with
        { FirstOwner = owner.Stream };
}
