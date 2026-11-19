using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaParticipantAdmissionTests
{
    [Fact]
    public void ShouldKeepLegacyLabelRequestPendingGivenNoPolicyDigest()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("user", "bdgrz");
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/identity/user", "generation", 1);
        var requestIntent = new DraftCriteriaEdition(edition, operation, "tsc", "edition_1");
        var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer,
            operation, CriteriaEdition.AddressOf(edition).ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(requestIntent), 1),
            DateTimeOffset.UtcNow, "policy", source, source)
        { Operator = source };
        var request = new CriteriaEditionDraftRequested(edition, operation, operation, 1, "tsc", "edition_1",
            actor, admission, null, null);

        // Act
        // Assert
        Assert.False(CriteriaParticipantAdmission.AllowsLabel(request));
        Assert.True(CriteriaParticipantAdmission.AllowsLabel(request with
        {
            Admission = admission with { FormatVersion = 1, PolicyContentSha256 = new string('a', 64) }
        }));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("operation")]
    [InlineData("target")]
    [InlineData("intent")]
    [InlineData("generation")]
    [InlineData("operator")]
    public void ShouldRejectCurrentFormatGivenLabelAdmissionDoesNotBindExactIntent(string change)
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("user", "bdgrz");
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/identity/user", "generation", 1);
        var intent = new DraftCriteriaEdition(edition, operation, "tsc", "edition_1");
        var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer,
            operation, CriteriaEdition.AddressOf(edition).ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1),
            DateTimeOffset.UtcNow, "policy", source, source)
        { FormatVersion = 1, PolicyContentSha256 = new string('a', 64), Operator = source };
        var request = new CriteriaEditionDraftRequested(edition, operation, operation, 1, "tsc", "edition_1", actor, admission, null, null);
        if (change == "actor")
            request = request with { Actor = new ActorAttribution("other", "bdgrz") };
        if (change == "operation")
            request = request with { OperationId = Uuid.CreateVersion4() };
        if (change == "target")
            request = request with { Admission = admission with { Target = "stream://bdgrz/criteria/other" } };
        if (change == "intent")
            request = request with { Label = "edition_2" };
        if (change == "generation")
            request = request with { ExecutionGeneration = 2 };
        if (change == "operator")
            request = request with { Admission = admission with { Operator = null } };

        // Act
        // Assert
        Assert.False(CriteriaParticipantAdmission.AllowsLabel(request));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("operation")]
    [InlineData("target")]
    [InlineData("witness")]
    [InlineData("generation")]
    public void ShouldRejectCurrentFormatGivenPublicationAdmissionDoesNotBindExactIntent(string change)
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var incarnation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("user", "bdgrz");
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/identity/user", "generation", 1);
        var intent = new BeginCriteriaPublication(edition, operation, "CC1.1", incarnation);
        var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer,
            operation, CriteriaEdition.AddressOf(edition).ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1),
            DateTimeOffset.UtcNow, "policy", source, source)
        { FormatVersion = 1, PolicyContentSha256 = new string('a', 64), Operator = source };
        var start = new CriteriaPublicationStarted(edition, Uuid.CreateVersion4(), operation, 1, 1,
            "CC1.1", incarnation, "edition_1", 1, Uuid.CreateVersion4(), actor, admission);
        Assert.True(CriteriaParticipantAdmission.AllowsHumanPublication(start));
        if (change == "actor")
            start = start with { Actor = new ActorAttribution("other", "bdgrz") };
        if (change == "operation")
            start = start with { PublicationAttemptId = Uuid.CreateVersion4() };
        if (change == "target")
            start = start with { Admission = admission with { Target = "stream://bdgrz/criteria/other" } };
        if (change == "witness")
            start = start with { WitnessIdentifier = "CC1.2" };
        if (change == "generation")
            start = start with { ExecutionGeneration = 2 };

        // Act

        // Assert
        Assert.False(CriteriaParticipantAdmission.AllowsHumanPublication(start));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldBindExactInitialOrRenameIntentGivenCurrentEvidence(bool rename)
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("user", "bdgrz");
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/identity/user", "generation", 1);
        var digest = rename
            ? AuthoredIntentDigest.Compute(new BeginCriteriaEditionRename(edition, operation, "edition_2"))
            : AuthoredIntentDigest.Compute(new ChooseInitialEditionLabel(edition, operation, "edition_2"));
        var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, operation,
            CriteriaEdition.AddressOf(edition).ToString(), CriteriaEdition.BindAdmissionDigest(digest, 1),
            DateTimeOffset.UtcNow, "policy", source, source)
        { FormatVersion = 1, PolicyContentSha256 = new string('a', 64), Operator = source };
        DomainEvent request = rename
            ? new CriteriaEditionRenameRequested(edition, operation, 1, "edition_1", 1, "edition_2", actor, admission)
            : new CriteriaInitialLabelRequested(edition, operation, 1, "edition_2", actor, admission);
        DomainEvent altered = request switch
        {
            CriteriaEditionRenameRequested value => value with { NewLabel = "edition_3" },
            CriteriaInitialLabelRequested value => value with { Label = "edition_3" },
            _ => throw new InvalidOperationException(),
        };

        // Act
        // Assert
        Assert.True(CriteriaParticipantAdmission.AllowsLabel(request));
        Assert.False(CriteriaParticipantAdmission.AllowsLabel(altered));
    }

    [Fact]
    public void ShouldAllowOnlyExactCompiledSeedBindingGivenNoHumanAdmission()
    {
        // Arrange
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var operation = PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "edition");
        var request = new CriteriaEditionDraftRequested(seed.EditionId, operation, operation, 1, seed.Framework,
            seed.EditionLabel, new ActorAttribution("criteria-seed-bootstrap", "system"), null,
            CriteriaSeedFingerprint.Version, CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries()));

        // Act
        // Assert
        Assert.True(CriteriaParticipantAdmission.AllowsLabel(request));
        Assert.False(CriteriaParticipantAdmission.AllowsLabel(request with { SeedFingerprint = new string('0', 64) }));
        Assert.False(CriteriaParticipantAdmission.AllowsLabel(request with { Actor = new ActorAttribution("other", "system") }));
    }
}
