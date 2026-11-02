using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class AuditObservationLifecycleTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ObservationId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRefuseResponseAndFindingLinkGivenStaleExpectedRevision()
    {
        // Arrange
        var content = Content();
        var requestId = Uuid.CreateVersion4();
        var observation = Replay(requestId, Recorded(content));

        // Act
        var response = observation.Respond(requestId, 0, 1, "Response", ActorId, "Recorder", Now.AddMinutes(1));
        var link = observation.LinkFinding(requestId, 0, Finding(content), ActorId, "Recorder", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(response).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(link).Code);
        Assert.Equal(1, observation.Revision);
        Assert.Empty(PendingOf(observation));
    }

    [Fact]
    public void ShouldRefuseResponseGivenSupersededSourceVersion()
    {
        // Arrange
        var content = Content();
        var recorded = Recorded(content);
        var correctionId = Uuid.CreateVersion4();
        var corrector = Replay(correctionId, recorded);
        Assert.Null(corrector.Correct(correctionId, 1, 1, content with { Wording = "Auditor correction" },
            "Received correction", ActorId, "Recorder", Now.AddMinutes(1)));
        DomainEvent[] history = [.. recorded, .. PendingOf(corrector)];
        var responseId = Uuid.CreateVersion4();
        var observation = Replay(responseId, history);

        // Act
        var failure = observation.Respond(responseId, 2, 1, "Response to old wording",
            ActorId, "Recorder", Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Null(observation.Current!.LatestManagementResponse);
        Assert.Empty(PendingOf(observation));
    }

    [Fact]
    public void ShouldRefuseCorrectionGivenUnchangedContentOrNonCurrentPreviousVersion()
    {
        // Arrange
        var content = Content();
        var requestId = Uuid.CreateVersion4();
        var observation = Replay(requestId, Recorded(content));

        // Act
        var unchanged = observation.Correct(requestId, 1, 1, content, "Received correction",
            ActorId, "Recorder", Now.AddMinutes(1));
        var nonCurrent = observation.Correct(requestId, 1, 2, content with { Wording = "Auditor correction" },
            "Received correction", ActorId, "Recorder", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(unchanged).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(nonCurrent).Code);
        Assert.Equal(1, observation.Current!.Received.Version);
        Assert.Empty(PendingOf(observation));
    }

    [Fact]
    public void ShouldReportMissingRecordGivenMutationBeforeRecording()
    {
        // Arrange
        var content = Content();
        var requestId = Uuid.CreateVersion4();
        var observation = new AuditObservation(TenantId, ProgramId, ObservationId, requestId);

        // Act
        var correction = observation.Correct(requestId, 0, 1, content with { Wording = "Correction" },
            "Received correction", ActorId, "Recorder", Now);
        var response = observation.Respond(requestId, 0, 1, "Response", ActorId, "Recorder", Now);
        var link = observation.LinkFinding(requestId, 0, Finding(content), ActorId, "Recorder", Now);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(correction).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(response).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(link).Code);
        Assert.Empty(PendingOf(observation));
    }

    [Fact]
    public void ShouldRefuseSecondRecordingGivenDifferentCommandOnExistingObservation()
    {
        // Arrange
        var content = Content();
        var requestId = Uuid.CreateVersion4();
        var observation = Replay(requestId, Recorded(content));

        // Act
        var failure = observation.Record(requestId, content with { Wording = "Different" },
            ActorId, "Recorder", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(content.Wording, observation.Current!.Received.Content.Wording);
        Assert.Empty(PendingOf(observation));
    }

    [Fact]
    public void ShouldKeepAbsentSourceMarkerGivenImportedRowAndRefuseBlankReference()
    {
        // Arrange
        var markerId = Uuid.CreateVersion4();
        var blankId = Uuid.CreateVersion4();
        var withMarker = new AuditObservation(TenantId, ProgramId, ObservationId, markerId);
        var withBlank = new AuditObservation(TenantId, ProgramId, ObservationId, blankId);

        // Act
        var marker = withMarker.Record(markerId, Content() with { SourceReference = "source_identifier_absent" },
            ActorId, "Recorder", Now);
        var blank = withBlank.Record(blankId, Content() with { SourceReference = "   " },
            ActorId, "Recorder", Now);

        // Assert
        Assert.Null(marker);
        Assert.Equal("source_identifier_absent", withMarker.Current!.Received.Content.SourceReference);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(blank).Code);
        Assert.Empty(PendingOf(withBlank));
    }

    [Fact]
    public void ShouldReplaceLatestResponseGivenSecondResponseOnCurrentSource()
    {
        // Arrange
        var content = Content();
        var firstId = Uuid.CreateVersion4();
        var recorded = Recorded(content);
        var first = Replay(firstId, recorded);
        Assert.Null(first.Respond(firstId, 1, 1, "First response", ActorId, "Recorder", Now.AddMinutes(1)));
        DomainEvent[] history = [.. recorded, .. PendingOf(first)];
        var secondId = Uuid.CreateVersion4();
        var observation = Replay(secondId, history);

        // Act
        var failure = observation.Respond(secondId, 2, 1, "Second response", ActorId, "Recorder", Now.AddMinutes(2));

        // Assert
        Assert.Null(failure);
        Assert.Equal(3, observation.Revision);
        Assert.Equal("Second response", observation.Current!.LatestManagementResponse!.Text);
    }

    static AuditObservation Replay(Uuid requestId, params DomainEvent[] events) =>
        new AggregateScenario<AuditObservation>(new AuditObservation(TenantId, ProgramId, ObservationId, requestId))
            .Given(events).Aggregate;

    static DomainEvent[] Recorded(AuditObservationContent content)
    {
        var requestId = Uuid.CreateVersion4();
        var observation = new AuditObservation(TenantId, ProgramId, ObservationId, requestId);
        Assert.Null(observation.Record(requestId, content, ActorId, "Recorder", Now));
        return PendingOf(observation);
    }

    static DomainEvent[] PendingOf(AuditObservation observation) =>
        [.. new AggregateScenario<AuditObservation>(observation).PendingEvents];

    static AuditObservationFindingReference Finding(AuditObservationContent content) =>
        new(TenantId, ProgramId, Uuid.CreateVersion4(), 1,
            new FindingSource("examination_item", ObservationId, "1", content.Wording));

    static AuditObservationContent Content() => new(Uuid.CreateVersion4(), "exception", "auditor",
        "auditor-message-1", "External auditor", Now, "Auditor wording", "medium",
        new ExaminationContentReference("type_ii_close", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('a', 64)),
        [new("control_occurrence", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('b', 64))]);
}
