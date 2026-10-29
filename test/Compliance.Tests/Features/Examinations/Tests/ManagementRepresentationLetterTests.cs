using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ManagementRepresentationLetterAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.ManagementRepresentationLetter;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ManagementRepresentationLetterTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid LetterId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid CEOId = Uuid.CreateVersion4();
    static readonly Uuid CISOId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldPreserveExternalSourceAndRequireEveryNamedSignerGivenRecordedLetter()
    {
        // Arrange
        var letter = new ManagementRepresentationLetterAggregate(TenantId, ProgramId, LetterId);
        var content = Content();

        // Act
        var recorded = letter.Record(content, CEOId, "CEO", Now);
        var wrongSigner = letter.Sign(letter.Revision, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new string('b', 64), Now.AddDays(1));
        var firstSignature = letter.Sign(letter.Revision, CEOId, Uuid.CreateVersion4(),
            new string('c', 64), Now.AddDays(1));

        // Assert
        Assert.Null(recorded);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(wrongSigner).Code);
        Assert.Null(firstSignature);
        Assert.Equal("auditor", letter.Current!.Content.SourceKind);
        Assert.Equal("auditor_request_442", letter.Current!.Content.SourceReference);
        Assert.Equal("awaiting_signatures", letter.Current.Status);
        Assert.Single(letter.Current.Signatures);
    }

    [Fact]
    public void ShouldMarkLetterSignedOnlyAfterAllRequiredAttributableSignaturesGivenLetter()
    {
        // Arrange
        var letter = NewLetter();
        Assert.Null(letter.Sign(letter.Revision, CEOId, Uuid.CreateVersion4(), new string('c', 64),
            Now.AddDays(1)));

        // Act
        var signed = letter.Sign(letter.Revision, CISOId, Uuid.CreateVersion4(),
            new string('d', 64), Now.AddDays(2));

        // Assert
        Assert.Null(signed);
        Assert.Equal("signed", letter.Current!.Status);
        Assert.Equal(2, letter.Current.Signatures.Count);
        Assert.NotEqual(letter.Current.Signatures[0].SignedArtifactId,
            letter.Current.Signatures[1].SignedArtifactId);
    }

    [Fact]
    public void ShouldRejectPlatformAuthoredAuditorOutcomeSourceGivenLetterContent()
    {
        // Arrange
        var letter = new ManagementRepresentationLetterAggregate(TenantId, ProgramId, LetterId);

        // Act
        var failure = letter.Record(Content() with { SourceKind = "product_calculated" },
            CEOId, "CEO", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, letter.Revision);
    }

    [Fact]
    public void ShouldReplayEveryRequiredSignatureGivenSignedLetter()
    {
        // Arrange
        var letter = NewLetter();
        Assert.Null(letter.Sign(letter.Revision, CEOId, Uuid.CreateVersion4(), new string('c', 64),
            Now.AddDays(1)));
        Assert.Null(letter.Sign(letter.Revision, CISOId, Uuid.CreateVersion4(), new string('d', 64),
            Now.AddDays(2)));
        var events = new AggregateScenario<ManagementRepresentationLetterAggregate>(letter)
            .PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ManagementRepresentationLetterAggregate>(
            new ManagementRepresentationLetterAggregate(TenantId, ProgramId, LetterId)).Given(events).Aggregate;

        // Assert
        Assert.Equal("signed", replay.Current!.Status);
        Assert.Equal(2, replay.Current.Signatures.Count);
        Assert.Equal(letter.Current!.Signatures.ToArray(), replay.Current.Signatures.ToArray());
    }

    [Fact]
    public void ShouldAcknowledgeOriginalCreationWithoutFactGivenLaterSignatureAndServerAttributionChanges()
    {
        // Arrange
        var letter = new ManagementRepresentationLetterAggregate(TenantId, ProgramId, LetterId);
        var scenario = new AggregateScenario<ManagementRepresentationLetterAggregate>(letter);
        var content = Content();
        Assert.Null(letter.Record(content, CEOId, "CEO", Now));
        Assert.Null(letter.Sign(letter.Revision, CEOId, Uuid.CreateVersion4(), new string('c', 64),
            Now.AddDays(1)));
        var before = scenario.PendingEvents.Count;

        // Act
        var retry = letter.Record(content, CEOId, "Changed display", Now.AddDays(2));
        var changedActor = letter.Record(content, CISOId, "CEO", Now);
        var changedIntent = letter.Record(content with { SourceReference = "Another source" }, CEOId, "CEO", Now);

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changedActor).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changedIntent).Code);
        Assert.Equal(before, scenario.PendingEvents.Count);
        Assert.Equal(2, letter.Revision);
        Assert.Equal("CEO", letter.Current!.RecordedByDisplay);
        Assert.Equal(Now, letter.Current.RecordedAt);
    }

    [Fact]
    public void ShouldRefuseReusedArtifactAndStaleSignatureWithoutFactGivenPriorSignature()
    {
        // Arrange
        var letter = NewLetter();
        var artifact = Uuid.CreateVersion4();
        Assert.Null(letter.Sign(letter.Revision, CEOId, artifact, new string('c', 64), Now.AddDays(1)));
        var scenario = new AggregateScenario<ManagementRepresentationLetterAggregate>(letter);
        var before = scenario.PendingEvents.Count;

        // Act
        var reused = letter.Sign(letter.Revision, CISOId, artifact, new string('d', 64), Now.AddDays(2));
        var stale = letter.Sign(1, CISOId, Uuid.CreateVersion4(), new string('d', 64), Now.AddDays(2));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(reused).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(before, scenario.PendingEvents.Count);
        Assert.Equal(2, letter.Revision);
        Assert.Equal("awaiting_signatures", letter.Current!.Status);
        Assert.Single(letter.Current.Signatures);
    }

    static ManagementRepresentationLetterAggregate NewLetter()
    {
        var letter = new ManagementRepresentationLetterAggregate(TenantId, ProgramId, LetterId);
        Assert.Null(letter.Record(Content(), CEOId, "CEO", Now));
        return letter;
    }

    static ManagementRepresentationLetterContent Content() => new(ExaminationId, "type_i",
        Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), new string('b', 64),
        Uuid.CreateVersion4(), 1, new string('c', 64), "auditor", "auditor_request_442",
        Uuid.CreateVersion4(), new string('a', 64), new DateOnly(2026, 9, 30),
        [new(CEOId, "CEO"), new(CISOId, "CISO")]);
}
