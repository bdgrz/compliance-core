using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaContentBoundsTests
{
    static readonly Uuid Edition = Uuid.CreateVersion4();
    static readonly Uuid Draft = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new("operator", "issuer");
    static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    static readonly string Fingerprint = new('a', 64);

    [Fact]
    public void ShouldFitEveryFixedSeedFieldGivenSourceBounds()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var entries = PlatformCriteriaCatalog.EnumerateSeedEntries().ToArray();

        // Act and assert
        Assert.All(entries, entry =>
        {
            Assert.True(CriteriaContentBounds.Identifier(entry.Identifier));
            Assert.True(CriteriaContentBounds.Summary(entry.Summary));
            if (entry.ParentIdentifier is not null)
                Assert.True(CriteriaContentBounds.Identifier(entry.ParentIdentifier));
        });
        Assert.True(CriteriaContentBounds.Text(edition.CoverageNote, CriteriaContentBounds.CoverageNoteBytes));
        Assert.True(CriteriaContentBounds.Text(edition.SourceUrl, CriteriaContentBounds.SourceUrlBytes));
        Assert.All(edition.SupportGaps, gap =>
        {
            Assert.True(CriteriaContentBounds.Text(gap.Code, CriteriaContentBounds.DisclosureCodeBytes));
            Assert.True(CriteriaContentBounds.Text(gap.Note, CriteriaContentBounds.DisclosureNoteBytes));
        });
    }

    [Fact]
    public void ShouldBoundIdentifierInUtf8BytesGivenMultibyteInput()
    {
        // Arrange
        var atLimit = new string('é', CriteriaContentBounds.IdentifierBytes / 2);
        var overLimit = atLimit + "é";

        // Act
        var owner = new CriteriaEntry(Edition, atLimit);
        var exception = Record.Exception(() => new CriteriaEntry(Edition, overLimit));

        // Assert
        Assert.Equal(CriteriaContentBounds.IdentifierBytes, Encoding.UTF8.GetByteCount(atLimit));
        Assert.NotEqual(Uuid.Empty, owner.Id);
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void ShouldRejectIdentifierGivenMalformedUtf16()
    {
        // Arrange
        var identifier = "criterion-\ud800";

        // Act
        var exception = Record.Exception(() => new CriteriaEntry(Edition, identifier));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void ShouldAcceptBoundaryAndRefuseExcessGivenUtf8Summary()
    {
        // Arrange
        var exactId = Uuid.CreateVersion4();
        var exactOwner = new CriteriaEntry(Edition, "CC6.1", exactId);
        var exact = Create(exactId, new string('é', CriteriaContentBounds.SummaryBytes / 2));
        var excessId = Uuid.CreateVersion4();
        var excessOwner = new CriteriaEntry(Edition, "CC6.2", excessId);
        var excess = Create(excessId, new string('é', CriteriaContentBounds.SummaryBytes / 2 + 1)) with
        { Identifier = "CC6.2" };
        var scenario = new AggregateScenario<CriteriaEntry>(excessOwner);
        var exactAdmission = Admission(exactOwner, exact);
        var excessAdmission = Admission(excessOwner, excess);

        // Act
        var accepted = exactOwner.Propose(exact, 1, Actor, exactAdmission, null, Claim(exact, Actor, exactAdmission));
        var refused = excessOwner.Propose(excess, 1, Actor, excessAdmission, null, Claim(excess, Actor, excessAdmission));

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(RequestErrorKind.Validation, refused.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseOversizedEscapedTextGivenSmallUtf8Input()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(Edition, "CC6.1", operation);
        var intent = Create(operation, new string('\u0001', 1500));
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var admission = Admission(owner, intent);

        // Act
        var result = owner.Propose(intent, 1, Actor, admission, null, Claim(intent, Actor, admission));

        // Assert
        Assert.True(Encoding.UTF8.GetByteCount(intent.Summary!) < CriteriaContentBounds.SummaryBytes);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseActualSerializedPayloadGivenSmallIndividualFields()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(Edition, "CC6.1", operation);
        var actor = new ActorAttribution(new string('a', 3300), "issuer");
        var intent = Create(operation, new string('x', 1900));
        var admission = Admission(owner, intent, actor);
        var claim = Claim(intent, actor, admission);
        var candidate = new CriterionChangeProposed(intent, 1,
            CriteriaEntry.IncarnationOf(Edition, "CC6.1", operation), actor, admission, null, claim.Metadata.EventId);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var result = owner.Propose(intent, 1, actor, admission, null, claim);

        // Assert
        Assert.True(CriteriaContentBounds.AdmissionFields(actor, admission));
        Assert.True(DomainEventPayload.Size(candidate) > CriteriaContentBounds.EventPayloadBytes);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("8192", result.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(scenario.PendingEvents);
        var replay = new CriteriaEntry(Edition, "CC6.1", operation);
        candidate.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), replay.Id, 1, Now));
        Assert.Throws<InvalidOperationException>(() => new AggregateScenario<CriteriaEntry>(replay).Given(candidate));
    }

    [Fact]
    public void ShouldRejectOversizedContentGivenReplayedCriterionProposal()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(Edition, "CC6.1", operation);
        var intent = Create(operation, new string('x', CriteriaContentBounds.SummaryBytes + 1));
        var proposed = new CriterionChangeProposed(intent, 1,
            CriteriaEntry.IncarnationOf(Edition, "CC6.1", operation), Actor, Admission(owner, intent), null,
            Uuid.CreateVersion4());
        proposed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1, Now));

        // Act and assert
        Assert.Throws<InvalidOperationException>(() => new AggregateScenario<CriteriaEntry>(owner).Given(proposed));
    }

    [Fact]
    public void ShouldRejectOversizedFenceGivenInvalidSummary()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(Edition, "CC6.1", operation);
        var intent = Create(operation, new string('x', CriteriaContentBounds.SummaryBytes + 1));
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var result = owner.FenceOperation(intent, 1, Actor, Admission(owner, intent));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectOversizedParentGivenPointOfFocusProposal()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(Edition, "focus", operation);
        var intent = Create(operation, "summary") with
        {
            Identifier = "focus",
            Kind = "point_of_focus",
            ParentIdentifier = new string('x', CriteriaContentBounds.IdentifierBytes + 1),
            ParentIncarnationId = Uuid.CreateVersion4(),
        };
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var admission = Admission(owner, intent);

        // Act
        var result = owner.Propose(intent, 1, Actor, admission, Uuid.CreateVersion4(), Claim(intent, Actor, admission));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldBoundDisclosureCodeGivenMultibyteInput()
    {
        // Arrange
        var exact = new string('é', CriteriaContentBounds.DisclosureCodeBytes / 2);
        var excessive = exact + "é";

        // Act
        var owner = new CriteriaSeedDisclosure(Edition, "privacy", exact);
        var failure = Record.Exception(() => new CriteriaSeedDisclosure(Edition, "privacy", excessive));

        // Assert
        Assert.NotEqual(Uuid.Empty, owner.Id);
        Assert.IsType<ArgumentException>(failure);
    }

    [Fact]
    public void ShouldAcceptDisclosureNoteAtUtf8LimitGivenBoundedEscapedPayload()
    {
        // Arrange
        var owner = new CriteriaSeedDisclosure(Edition, "privacy", "partial");
        var note = new string('é', CriteriaContentBounds.DisclosureNoteBytes / 2);

        // Act
        var result = owner.Record(Uuid.CreateVersion4(), "seed-v1", Fingerprint, Uuid.CreateVersion4(), note, Actor);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.Single(new AggregateScenario<CriteriaSeedDisclosure>(owner).PendingEvents);
        Assert.True(DomainEventPayload.Size(fact) <= CriteriaContentBounds.EventPayloadBytes);
    }

    [Fact]
    public void ShouldRejectOversizedDisclosureGivenNewAndReplayedFact()
    {
        // Arrange
        var owner = new CriteriaSeedDisclosure(Edition, "privacy", "partial");
        var oversized = new string('x', CriteriaContentBounds.DisclosureNoteBytes + 1);
        var operation = Uuid.CreateVersion4();
        var request = Uuid.CreateVersion4();
        var replayOwner = new CriteriaSeedDisclosure(Edition, "privacy", "partial");
        var fact = new CriteriaSeedDisclosureRecorded(Edition, "privacy", "partial", operation, "seed-v1",
            Fingerprint, request, oversized, Actor);
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), replayOwner.Id, 1, Now));

        // Act
        var result = owner.Record(operation, "seed-v1", Fingerprint, request, oversized, Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CriteriaSeedDisclosure>(owner).PendingEvents);
        Assert.Throws<InvalidOperationException>(() =>
            new AggregateScenario<CriteriaSeedDisclosure>(replayOwner).Given(fact));
    }

    [Fact]
    public void ShouldRejectOversizedSeedMetadataGivenNewAndReplayedFact()
    {
        // Arrange
        var owner = new CriteriaSeedMetadata(Edition);
        var replayOwner = new CriteriaSeedMetadata(Edition);
        var operation = Uuid.CreateVersion4();
        var request = Uuid.CreateVersion4();
        var oversizedUrl = new string('u', CriteriaContentBounds.SourceUrlBytes + 1);
        var fact = new CriteriaSeedMetadataRecorded(Edition, operation, "seed-v1", Fingerprint, request,
            "tsc", Now, "coverage", oversizedUrl, "identifiers_and_original_summaries", true, Actor);
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), replayOwner.Id, 1, Now));

        // Act
        var result = owner.Record(operation, "seed-v1", Fingerprint, request, "tsc", Now, "coverage",
            oversizedUrl, "identifiers_and_original_summaries", true, Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CriteriaSeedMetadata>(owner).PendingEvents);
        Assert.Throws<InvalidOperationException>(() =>
            new AggregateScenario<CriteriaSeedMetadata>(replayOwner).Given(fact));
    }

    [Fact]
    public void ShouldAllowLongAttributionGivenCompleteEventWithinBudget()
    {
        // Arrange
        var owner = new CriteriaSeedMetadata(Edition);
        var actor = new ActorAttribution(new string('s', 1000), "https://" + new string('i', 500));

        // Act
        var result = owner.Record(Uuid.CreateVersion4(), "seed-v1", Fingerprint, Uuid.CreateVersion4(), "tsc",
            Now, "coverage", "https://example.test", "identifiers_and_original_summaries", true, actor);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(actor, Assert.IsType<CriteriaSeedMetadataRecorded>(
            Assert.Single(new AggregateScenario<CriteriaSeedMetadata>(owner).PendingEvents)).RecordedBy);
    }

    static CriterionChangeIntent Create(Uuid operation, string summary) =>
        new(Edition, Draft, "CC6.1", operation, 0, "create", "criterion", "security", null, null, summary);

    // A target-only fixture: callers in production must source-verify the claim.
    static CriterionEntryOperationClaimed Claim(CriterionChangeIntent intent, ActorAttribution actor,
        AccessAdmissionEvidence admission)
    {
        var claim = new CriterionEntryOperationClaimed(intent, null, 1, actor, admission);
        claim.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            CriteriaEntryOperation.IdentityOf(intent.EditionId, intent.Identifier, intent.OperationId), 1, Now));
        return claim;
    }

    static AccessAdmissionEvidence Admission(CriteriaEntry owner, CriterionChangeIntent intent,
        ActorAttribution? actor = null)
    {
        actor ??= Actor;
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
        return new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, intent.OperationId,
            owner.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1),
            Now, "policy", source, source)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = source
        };
    }
}
