using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceArtifactTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly ActorReference Collector = ActorReference.ForMember(Uuid.CreateVersion4(), "Collector");
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    const string Sha256 = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact]
    public void ShouldRegisterPendingInspectionGivenValidMetadata()
    {
        // Arrange
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());

        // Act
        var result = artifact.Register(Content(), Sha256, 4, Collector, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, artifact.State);
        Assert.Equal(4, artifact.ContentLength);
        var registered = Assert.IsType<EvidenceArtifactRegistered>(Assert.Single(
            new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents));
        Assert.Equal("Quarterly access review export", registered.Content.Title);
        Assert.Equal(Sha256, registered.ContentSha256);
        Assert.Equal(Collector, registered.Collector);
    }

    [Theory]
    [InlineData(" ", "confidential", 0, 4, Sha256)]
    [InlineData("Export", "secret", 0, 4, Sha256)]
    [InlineData("Export", "internal", -1, 4, Sha256)]
    [InlineData("Export", "internal", 0, 0, Sha256)]
    [InlineData("Export", "internal", 0, 4, "ABC")]
    public void ShouldRejectBeforeAppendGivenInvalidMetadata(string title, string handlingClass,
        int periodEndOffsetDays, long length, string sha256)
    {
        // Arrange
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());
        var content = Content() with
        {
            Title = title,
            HandlingClass = handlingClass,
            PeriodEnd = Content().PeriodStart.AddDays(periodEndOffsetDays),
        };

        // Act
        var result = artifact.Register(content, sha256, length, Collector, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents);
    }

    [Fact]
    public void ShouldReplayIdenticalAndRejectChangedRegistrationGivenRegisteredArtifact()
    {
        // Arrange
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());
        Assert.True(artifact.Register(Content(), Sha256, 4, Collector, Now).IsSuccess);

        // Act
        var replay = artifact.Register(Content(), Sha256, 4, Collector, Now);
        var changed = artifact.Register(Content() with { Title = "Other" }, Sha256, 4, Collector, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Single(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents);
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.Clean, "available", null)]
    [InlineData(EvidenceInspectionOutcome.Malware, "quarantined", "malware")]
    [InlineData(EvidenceInspectionOutcome.SecretDetected, "rejected", "secret_detected")]
    [InlineData(EvidenceInspectionOutcome.Invalid, "rejected", "invalid")]
    public void ShouldMoveToInspectedStateGivenInspectionOutcome(EvidenceInspectionOutcome outcome,
        string state, string? reason)
    {
        // Arrange
        var artifact = Registered();

        // Act
        var failure = artifact.RecordInspection(outcome, Now.AddMinutes(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal(state, artifact.State);
        Assert.Equal(reason, artifact.StateReason);
    }

    [Fact]
    public void ShouldRefuseSecondInspectionGivenAlreadyInspected()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now));

        // Act
        var failure = artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("available", artifact.State);
    }

    [Fact]
    public void ShouldReleaseMalwareFalsePositiveGivenQuarantinedArtifact()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));
        var admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Admin");

        // Act
        var failure = artifact.ReleaseQuarantine(artifact.Revision, Uuid.CreateVersion4(),
            Now.AddMinutes(1), "Rescan clean; vendor false positive", admin, Now.AddMinutes(2));

        // Assert
        Assert.Null(failure);
        Assert.Equal("available", artifact.State);
        Assert.Null(artifact.StateReason);
    }

    [Fact]
    public void ShouldNeverReleaseGivenSecretDetected()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.SecretDetected, Now));

        // Act
        var failure = artifact.ReleaseQuarantine(artifact.Revision, Uuid.CreateVersion4(),
            Now, "Looks fine", Collector, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("rejected", artifact.State);
    }

    [Fact]
    public void ShouldRequireRationaleGivenQuarantineRelease()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));

        // Act
        var failure = artifact.ReleaseQuarantine(artifact.Revision, Uuid.CreateVersion4(),
            Now, " ", Collector, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("quarantined", artifact.State);
    }

    [Fact]
    public void ShouldRefuseInspectionGivenUnregisteredArtifact()
    {
        // Arrange
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());

        // Act
        var failure = artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, failure!.Code);
        Assert.Empty(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenInspectionBeforeRegistration()
    {
        // Arrange
        var artifact = Registered();

        // Act
        var failure = artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now.AddMinutes(-1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, artifact.State);
        Assert.DoesNotContain(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents,
            e => e is EvidenceArtifactInspected);
    }

    [Fact]
    public void ShouldAppendNothingGivenUnknownInspectionOutcome()
    {
        // Arrange
        var artifact = Registered();

        // Act
        var failure = artifact.RecordInspection((EvidenceInspectionOutcome)999, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
        Assert.DoesNotContain(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents,
            e => e is EvidenceArtifactInspected);
    }

    [Fact]
    public void ShouldTreatSameInspectionAsNoOpGivenCommittedInspectionAndLaterRelease()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));
        Assert.Null(artifact.ReleaseQuarantine(artifact.Revision, Uuid.CreateVersion4(), Now.AddMinutes(1),
            "Rescan clean", Admin(), Now.AddMinutes(2)));
        var pending = new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents.Count;

        // Act
        var retry = artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now);
        var changed = artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now);

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, changed!.Code);
        Assert.Equal(pending, new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents.Count);
    }

    [Fact]
    public void ShouldIssueDownloadAndAdvanceRevisionGivenAvailableArtifact()
    {
        // Arrange
        var artifact = Available();
        var revision = artifact.Revision;

        // Act
        var failure = Download(artifact, revision, Uuid.CreateVersion4());

        // Assert
        Assert.Null(failure);
        Assert.Equal(revision + 1, artifact.Revision);
        var issued = Assert.Single(
            new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents.OfType<EvidenceArtifactDownloadIssued>());
        Assert.Equal(revision + 1, issued.Revision);
        Assert.Equal(Sha256, issued.ContentSha256);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionGivenCompetingDownload()
    {
        // Arrange
        var artifact = Available();
        var revision = artifact.Revision;
        Assert.Null(Download(artifact, revision, Uuid.CreateVersion4()));

        // Act
        var failure = Download(artifact, revision, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, failure!.Code);
        Assert.Equal(revision + 1, artifact.Revision);
    }

    [Fact]
    public void ShouldRefuseDownloadGivenArtifactThatIsNotAvailable()
    {
        // Arrange
        var quarantined = Registered();
        Assert.Null(quarantined.RecordInspection(EvidenceInspectionOutcome.Malware, Now));
        var pending = Registered();

        // Act
        var quarantinedFailure = Download(quarantined, quarantined.Revision, Uuid.CreateVersion4());
        var pendingFailure = Download(pending, pending.Revision, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, quarantinedFailure!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, pendingFailure!.Code);
        Assert.DoesNotContain(new AggregateScenario<EvidenceArtifact>(quarantined).PendingEvents,
            e => e is EvidenceArtifactDownloadIssued);
        Assert.DoesNotContain(new AggregateScenario<EvidenceArtifact>(pending).PendingEvents,
            e => e is EvidenceArtifactDownloadIssued);
    }

    [Theory]
    [InlineData("other-hash", 4)]
    [InlineData(Sha256, 5)]
    public void ShouldAppendNothingGivenDownloadOfDifferentContent(string sha256, long length)
    {
        // Arrange
        var artifact = Available();

        // Act
        var failure = artifact.RecordDownload(artifact.Revision, Uuid.CreateVersion4(), "Audit",
            Admin(), Now.AddHours(1), sha256, length);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
        Assert.DoesNotContain(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents,
            e => e is EvidenceArtifactDownloadIssued);
    }

    [Fact]
    public void ShouldAppendNothingGivenDownloadWithoutPurpose()
    {
        // Arrange
        var artifact = Available();

        // Act
        var failure = artifact.RecordDownload(artifact.Revision, Uuid.CreateVersion4(), " ",
            Admin(), Now.AddHours(1), Sha256, 4);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
        Assert.DoesNotContain(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents,
            e => e is EvidenceArtifactDownloadIssued);
    }

    [Fact]
    public void ShouldTreatSameReleaseAsNoOpGivenCommittedReleaseAndLaterDownload()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));
        var requestId = Uuid.CreateVersion4();
        var admin = Admin();
        var revision = artifact.Revision;
        Assert.Null(artifact.ReleaseQuarantine(revision, requestId, Now.AddMinutes(1), "Rescan clean", admin,
            Now.AddMinutes(2)));
        Assert.Null(Download(artifact, artifact.Revision, Uuid.CreateVersion4()));
        var pending = new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents.Count;

        // Act
        var retry = artifact.ReleaseQuarantine(revision, requestId, Now.AddMinutes(1), " Rescan clean ", admin,
            Now.AddMinutes(2));

        // Assert
        Assert.Null(retry);
        Assert.Equal(pending, new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseChangedReleaseGivenCommittedRelease()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));
        var requestId = Uuid.CreateVersion4();
        var revision = artifact.Revision;
        var admin = Admin();
        Assert.Null(artifact.ReleaseQuarantine(revision, requestId, Now.AddMinutes(1), "Rescan clean", admin,
            Now.AddMinutes(2)));
        var pending = new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents.Count;

        // Act
        var changedRationale = artifact.ReleaseQuarantine(revision, requestId, Now.AddMinutes(1), "Different",
            admin, Now.AddMinutes(2));
        var otherRequest = artifact.ReleaseQuarantine(revision, Uuid.CreateVersion4(), Now.AddMinutes(1),
            "Rescan clean", admin, Now.AddMinutes(2));
        var changedRescan = artifact.ReleaseQuarantine(revision, requestId, Now.AddMinutes(1).AddSeconds(1),
            "Rescan clean", admin, Now.AddMinutes(2));
        var otherDecider = artifact.ReleaseQuarantine(revision, requestId, Now.AddMinutes(1), "Rescan clean",
            Admin(), Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, changedRationale!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, otherRequest!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, otherDecider!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, changedRescan!.Code);
        Assert.Equal(pending, new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionGivenQuarantineRelease()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));

        // Act
        var failure = artifact.ReleaseQuarantine(artifact.Revision - 1, Uuid.CreateVersion4(), Now.AddMinutes(1),
            "Rescan clean", Admin(), Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, failure!.Code);
        Assert.Equal(EvidenceArtifactStates.Quarantined, artifact.State);
    }

    [Fact]
    public void ShouldAppendNothingGivenReleaseRescanBeforeInspection()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now.AddMinutes(5)));

        // Act
        var failure = artifact.ReleaseQuarantine(artifact.Revision, Uuid.CreateVersion4(), Now.AddMinutes(1),
            "Rescan clean", Admin(), Now.AddMinutes(6));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
        Assert.Equal(EvidenceArtifactStates.Quarantined, artifact.State);
    }

    [Fact]
    public void ShouldAppendNothingGivenReleaseByNonMemberActor()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));
        var system = ActorReference.ForSystemProcess("scanner", "Scanner");

        // Act
        var failure = artifact.ReleaseQuarantine(artifact.Revision, Uuid.CreateVersion4(), Now.AddMinutes(1),
            "Rescan clean", system, Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
        Assert.Equal(EvidenceArtifactStates.Quarantined, artifact.State);
    }

    [Fact]
    public void ShouldRestoreAvailableArtifactGivenValidHistory()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var probe = new EvidenceArtifact(TenantId, id);
        var admin = Admin();
        var facts = new DomainEvent[]
        {
            DomainEventSeed.Attach(new EvidenceArtifactRegistered(TenantId, id, Content(), Sha256, 4, Collector, Now),
                probe.Id, 1),
            DomainEventSeed.Attach(new EvidenceArtifactInspected(TenantId, id, "quarantined", "malware", Now),
                probe.Id, 2),
            DomainEventSeed.Attach(new EvidenceArtifactQuarantineReleased(TenantId, id, 3, Uuid.CreateVersion4(),
                "clean", Now.AddMinutes(1), "Rescan clean", admin, Now.AddMinutes(2)), probe.Id, 3),
            DomainEventSeed.Attach(new EvidenceArtifactDownloadIssued(TenantId, id, 4, Uuid.CreateVersion4(), Sha256, 4,
                "Audit", admin, Now.AddHours(1)), probe.Id, 4),
        };

        // Act
        var artifact = new AggregateScenario<EvidenceArtifact>(new EvidenceArtifact(TenantId, id)).Given(facts)
            .Aggregate;

        // Assert
        Assert.Equal(EvidenceArtifactStates.Available, artifact.State);
        Assert.Null(artifact.StateReason);
        Assert.Equal(4, artifact.Revision);
        Assert.NotNull(artifact.QuarantineRelease);
    }

    [Fact]
    public void ShouldAddressStreamByTenantAndArtifactGivenIdentity()
    {
        // Arrange
        var id = Uuid.CreateVersion4();

        // Act
        var first = new EvidenceArtifact(TenantId, id);
        var second = new EvidenceArtifact(TenantId, id);
        var otherTenant = new EvidenceArtifact(Uuid.CreateVersion4(), id);

        // Assert
        Assert.Equal(first.Stream, second.Stream);
        Assert.NotEqual(first.Stream, otherTenant.Stream);
    }

    static ActorReference Admin() => ActorReference.ForMember(Uuid.CreateVersion4(), "Admin");

    static EvidenceArtifact Available()
    {
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now));
        return artifact;
    }

    static CommandFailure? Download(EvidenceArtifact artifact, long revision, Uuid requestId) =>
        artifact.RecordDownload(revision, requestId, "Audit", Admin(), Now.AddHours(1), Sha256, 4);

    static EvidenceArtifact Registered()
    {
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());
        Assert.True(artifact.Register(Content(), Sha256, 4, Collector, Now).IsSuccess);
        return artifact;
    }

    static EvidenceArtifactContent Content() =>
        new("Quarterly access review export", null, "system_export", "Okta admin console",
            Now.AddHours(-2), new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), "confidential");
}
