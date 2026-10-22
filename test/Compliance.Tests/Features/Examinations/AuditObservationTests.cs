using System.Text.Json;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class AuditObservationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ObservationId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldPreserveVerbatimAuditorWordingGivenReceivedException()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var observation = New(requestId);
        var content = Content() with { Wording = "  Auditor wording.\nSecond line.  " };

        // Act
        var failure = observation.Record(requestId, content, ActorId, "Recorder", Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(content.Wording, observation.Current!.Received.Content.Wording);
        Assert.Equal("auditor", observation.Current.Received.Content.SourceKind);
        Assert.Equal(1, observation.Revision);
        Assert.IsType<AuditObservationRecorded>(Assert.Single(Events(observation)));
    }

    [Fact]
    public void ShouldPreserveSourceLineageGivenAuditorCorrectionAndManagementResponse()
    {
        // Arrange
        var content = Content();
        var observation = Recorded(content);
        var recorded = Assert.IsType<AuditObservationRecorded>(Events(observation)[0]);
        var correctionId = Uuid.CreateVersion4();
        observation = Replay(correctionId, Events(observation));
        var corrected = content with { Wording = " Corrected by the auditor. ", SourceReference = "auditor-message-2" };
        Assert.Null(observation.Correct(correctionId, 1, 1, corrected, "Received correction",
            ActorId, "Recorder", Now.AddMinutes(1)));
        var responseId = Uuid.CreateVersion4();
        observation = Replay(responseId, Events(observation, recorded));

        // Act
        var failure = observation.Respond(responseId, 2, 2, "Management response",
            ActorId, "Recorder", Now.AddMinutes(2));

        // Assert
        Assert.Null(failure);
        Assert.Equal(content.Wording, recorded.Received.Content.Wording);
        Assert.Equal(corrected.Wording, observation.Current!.Received.Content.Wording);
        Assert.Equal(2, observation.Current.Received.Version);
        Assert.Equal("Management response", observation.Current.LatestManagementResponse!.Text);
        Assert.Equal(2, observation.Current.LatestManagementResponse.SourceVersion);
    }

    [Fact]
    public void ShouldRejectChangedRetryAndStaleCorrectionGivenRecordedSource()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var content = Content();
        var observation = New(requestId);
        Assert.Null(observation.Record(requestId, content, ActorId, "Recorder", Now));

        // Act
        var changed = observation.Record(requestId, content with { Wording = "Changed" },
            ActorId, "Recorder", Now.AddMinutes(1));
        var correctionId = Uuid.CreateVersion4();
        observation = Replay(correctionId, Events(observation));
        var stale = observation.Correct(correctionId, 0, 1, content with { Wording = "Correction" },
            "Received correction", ActorId, "Recorder", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(1, observation.Revision);
        Assert.Empty(Events(observation));
    }

    [Fact]
    public void ShouldRejectFindingLinkGivenWrongProgramOrSourceVersion()
    {
        // Arrange
        var content = Content();
        var source = Recorded(content);
        var requestId = Uuid.CreateVersion4();
        var observation = Replay(requestId, Events(source));
        var finding = Finding(content) with { ProgramId = Uuid.CreateVersion4() };

        // Act
        var wrongProgram = observation.LinkFinding(requestId, 1, finding, ActorId, "Recorder", Now.AddMinutes(1));
        var wrongVersion = observation.LinkFinding(requestId, 1,
            Finding(content) with { Source = new FindingSource("examination_item", ObservationId, "2", content.Wording) },
            ActorId, "Recorder", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(wrongProgram).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(wrongVersion).Code);
        Assert.Empty(Events(observation));
    }

    [Fact]
    public void ShouldRememberExactOlderCommandGivenLaterCorrectionResponseAndFindingLink()
    {
        // Arrange
        var content = Content();
        var firstId = Uuid.CreateVersion4();
        var observation = New(firstId);
        Assert.Null(observation.Record(firstId, content, ActorId, "Recorder", Now));
        var history = Events(observation).ToList();
        var correctionId = Uuid.CreateVersion4();
        var corrected = content with { Wording = "Auditor correction", SourceReference = "auditor-message-2" };
        observation = Replay(correctionId, history.ToArray());
        Assert.Null(observation.Correct(correctionId, 1, 1, corrected, "Received correction",
            ActorId, "Recorder", Now.AddMinutes(1)));
        history.AddRange(Events(observation));
        var responseId = Uuid.CreateVersion4();
        observation = Replay(responseId, history.ToArray());
        Assert.Null(observation.Respond(responseId, 2, 2, "Management response", ActorId, "Recorder", Now.AddMinutes(2)));
        history.AddRange(Events(observation));
        var linkId = Uuid.CreateVersion4();
        var finding = Finding(corrected) with { Source = new FindingSource("examination_item", ObservationId, "2", corrected.Wording) };
        observation = Replay(linkId, history.ToArray());
        Assert.Null(observation.LinkFinding(linkId, 3, finding, ActorId, "Recorder", Now.AddMinutes(3)));
        history.AddRange(Events(observation));

        // Act
        var creationRetry = Replay(firstId, history.ToArray());
        var correctionRetry = Replay(correctionId, history.ToArray());
        var responseRetry = Replay(responseId, history.ToArray());
        var linkRetry = Replay(linkId, history.ToArray());

        // Assert
        Assert.Null(creationRetry.Record(firstId, content, ActorId, "Updated display", Now.AddHours(1)));
        Assert.Null(correctionRetry.Correct(correctionId, 1, 1, corrected, "Received correction", ActorId, "Updated display", Now.AddHours(1)));
        Assert.Null(responseRetry.Respond(responseId, 2, 2, "Management response", ActorId, "Updated display", Now.AddHours(1)));
        Assert.Null(linkRetry.LinkFinding(linkId, 3, finding, ActorId, "Updated display", Now.AddHours(1)));
        Assert.Empty(Events(creationRetry));
        Assert.Empty(Events(correctionRetry));
        Assert.Empty(Events(responseRetry));
        Assert.Empty(Events(linkRetry));
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(responseRetry.Respond(responseId,
            2, 2, "Different response", ActorId, "Recorder", Now.AddHours(1))).Code);
        Assert.Equal(4, linkRetry.Revision);
    }

    [Fact]
    public void ShouldRejectMutationGivenAbsentOrMismatchedCommandSelector()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var unselected = new AuditObservation(TenantId, ProgramId, ObservationId);
        var selected = New(Uuid.CreateVersion4());

        // Act
        var absent = unselected.Record(requestId, Content(), ActorId, "Recorder", Now);
        var mismatched = selected.Record(requestId, Content(), ActorId, "Recorder", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(absent).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(mismatched).Code);
        Assert.Empty(Events(unselected));
        Assert.Empty(Events(selected));
    }

    [Fact]
    public void ShouldRejectMalformedOrCrossTenantEventGivenReplay()
    {
        // Arrange
        var observation = Recorded(Content());
        var recorded = Assert.IsType<AuditObservationRecorded>(Assert.Single(Events(observation)));
        var wrongTenant = recorded with { TenantId = Uuid.CreateVersion4() };
        var tampered = recorded with { Received = recorded.Received with { ContentSha256 = new string('f', 64) } };

        // Act
        var crossTenant = Record.Exception(() => Replay(recorded.Received.RequestId, wrongTenant));
        var invalidDigest = Record.Exception(() => Replay(recorded.Received.RequestId, tampered));

        // Assert
        Assert.IsType<InvalidOperationException>(crossTenant);
        Assert.IsType<InvalidOperationException>(invalidDigest);
    }

    [Fact]
    public void ShouldRejectOversizedSerializedEventGivenOtherwiseBoundedSourceFields()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var observation = New(requestId);
        var content = Content() with
        {
            Wording = new string('\u4e00', 4000),
            SourceReference = new string('\u4e00', 1000),
            AuditorAuthor = new string('\u4e00', 200),
            AffectedRecords = Enumerable.Range(0, 64).Select(_ => new ExaminationContentReference(
                "control_occurrence", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('a', 64))).ToArray(),
        };

        // Act
        var failure = observation.Record(requestId, content, ActorId, "Recorder", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, observation.Revision);
        Assert.Empty(Events(observation));
    }

    [Fact]
    public void ShouldPreserveEventAndSelectedReceiptGivenCallerMutatesSourceCollection()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var references = Content().AffectedRecords.ToList();
        var originalReference = references[0];
        var content = Content() with { AffectedRecords = references };
        var observation = New(requestId);
        Assert.Null(observation.Record(requestId, content, ActorId, "Recorder", Now));
        var recorded = Assert.IsType<AuditObservationRecorded>(Assert.Single(Events(observation)));
        var serialized = JsonSerializer.Serialize(recorded);
        var replay = Replay(requestId, recorded);

        // Act
        references[0] = originalReference with { RecordId = Uuid.CreateVersion4() };
        references.Clear();
        var retry = replay.Record(requestId, content, ActorId, "Recorder", Now.AddHours(1));
        var changed = replay.Record(requestId, content with { AffectedRecords = references },
            ActorId, "Recorder", Now.AddHours(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(originalReference, Assert.Single(replay.Current!.Received.Content.AffectedRecords));
        Assert.Equal(serialized, JsonSerializer.Serialize(recorded));
        Assert.Empty(Events(replay));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    public void ShouldRejectRecordingGivenEmptyAggregateScope(string missing)
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var observation = new AuditObservation(missing == "tenant" ? Uuid.Empty : TenantId,
            missing == "program" ? Uuid.Empty : ProgramId,
            ObservationId, requestId);

        // Act
        var failure = observation.Record(requestId, Content(), ActorId, "Recorder", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(Events(observation));
    }

    [Fact]
    public void ShouldRejectConstructionGivenEmptyObservationIdentity()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();

        // Act
        var failure = Record.Exception(() => new AuditObservation(TenantId, ProgramId, Uuid.Empty, requestId));

        // Assert
        Assert.IsType<ArgumentException>(failure);
    }

    [Fact]
    public void ShouldRejectRepeatedSelectedCommandIdentityGivenOtherwiseValidLaterFact()
    {
        // Arrange
        var content = Content();
        var recorded = Assert.IsType<AuditObservationRecorded>(Assert.Single(Events(Recorded(content))));
        var requestId = Uuid.CreateVersion4();
        var observation = Replay(requestId, recorded);
        Assert.Null(observation.Respond(requestId, 1, 1, "Response", ActorId, "Recorder", Now.AddMinutes(1)));
        var response = Assert.IsType<AuditObservationManagementResponseRecorded>(Assert.Single(Events(observation)));
        var duplicate = response with
        {
            Revision = 3,
            Response = response.Response with { Text = "Another response", RecordedAt = Now.AddMinutes(2) },
            OccurredAt = Now.AddMinutes(2),
        };

        // Act
        var failure = Record.Exception(() => Replay(requestId, recorded, response, duplicate));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void ShouldReplaySerializedFactsGivenCorrectedResponseAndFindingLink()
    {
        // Arrange
        var content = Content();
        var observation = Recorded(content);
        var history = Events(observation).ToList();
        var requestId = Uuid.CreateVersion4();
        observation = Replay(requestId, history.ToArray());
        var corrected = content with { Wording = " Exact auditor correction. " };
        Assert.Null(observation.Correct(requestId, 1, 1, corrected, "Correction", ActorId, "Recorder", Now.AddMinutes(1)));
        history.AddRange(Events(observation));
        requestId = Uuid.CreateVersion4();
        observation = Replay(requestId, history.ToArray());
        Assert.Null(observation.Respond(requestId, 2, 2, "Response", ActorId, "Recorder", Now.AddMinutes(2)));
        history.AddRange(Events(observation));
        requestId = Uuid.CreateVersion4();
        observation = Replay(requestId, history.ToArray());
        var finding = Finding(corrected) with { Source = new FindingSource("examination_item", ObservationId, "2", corrected.Wording) };
        Assert.Null(observation.LinkFinding(requestId, 3, finding, ActorId, "Recorder", Now.AddMinutes(3)));
        history.AddRange(Events(observation));
        var serializedHistory = history.Select(static ev => ev switch
        {
            AuditObservationRecorded recorded => (DomainEvent)JsonSerializer.Deserialize<AuditObservationRecorded>(JsonSerializer.Serialize(recorded))!,
            AuditObservationCorrected correction => JsonSerializer.Deserialize<AuditObservationCorrected>(JsonSerializer.Serialize(correction))!,
            AuditObservationManagementResponseRecorded response => JsonSerializer.Deserialize<AuditObservationManagementResponseRecorded>(JsonSerializer.Serialize(response))!,
            AuditObservationFindingLinked linked => JsonSerializer.Deserialize<AuditObservationFindingLinked>(JsonSerializer.Serialize(linked))!,
            _ => throw new InvalidOperationException("Unexpected observation fact."),
        }).ToArray();

        // Act
        var replay = Replay(requestId, serializedHistory);

        // Assert
        Assert.Equal(4, replay.Revision);
        Assert.Equal(corrected.Wording, replay.Current!.Received.Content.Wording);
        Assert.Equal("Response", replay.Current.LatestManagementResponse!.Text);
        Assert.Equal(finding, replay.Current.LatestFindingLink!.Finding);
    }

    [Fact]
    public void ShouldRejectSourceRebindingOrUnsupportedAuthorshipGivenAuditorCorrection()
    {
        // Arrange
        var content = Content();
        var recorded = Recorded(content);
        var requestId = Uuid.CreateVersion4();
        var observation = Replay(requestId, Events(recorded));

        // Act
        var rebound = observation.Correct(requestId, 1, 1,
            content with { ExaminationEngagementId = Uuid.CreateVersion4(), Wording = "Correction" },
            "Received correction", ActorId, "Recorder", Now.AddMinutes(1));
        var platformAuthored = observation.Correct(requestId, 1, 1,
            content with { SourceKind = "management", Wording = "Correction" },
            "Received correction", ActorId, "Recorder", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(rebound).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(platformAuthored).Code);
        Assert.Empty(Events(observation));
    }

    [Fact]
    public void ShouldRejectMalformedCorrectionResponseAndFindingLinkGivenReplay()
    {
        // Arrange
        var content = Content();
        var observation = Recorded(content);
        var history = Events(observation).ToList();
        var requestId = Uuid.CreateVersion4();
        observation = Replay(requestId, history.ToArray());
        Assert.Null(observation.Correct(requestId, 1, 1,
            content with { Wording = "Auditor correction" }, "Received correction",
            ActorId, "Recorder", Now.AddMinutes(1)));
        var correction = Assert.IsType<AuditObservationCorrected>(Assert.Single(Events(observation)));
        history.Add(correction);
        requestId = Uuid.CreateVersion4();
        observation = Replay(requestId, history.ToArray());
        Assert.Null(observation.Respond(requestId, 2, 2, "Response", ActorId, "Recorder", Now.AddMinutes(2)));
        var response = Assert.IsType<AuditObservationManagementResponseRecorded>(Assert.Single(Events(observation)));
        history.Add(response);
        requestId = Uuid.CreateVersion4();
        observation = Replay(requestId, history.ToArray());
        var finding = Finding(content) with { Source = new FindingSource("examination_item", ObservationId, "2", "Auditor correction") };
        Assert.Null(observation.LinkFinding(requestId, 3, finding, ActorId, "Recorder", Now.AddMinutes(3)));
        var link = Assert.IsType<AuditObservationFindingLinked>(Assert.Single(Events(observation)));

        // Act
        var invalidCorrection = Record.Exception(() => Replay(correction.Received.RequestId,
            history[0], correction with { PreviousSourceVersion = 0 }));
        var invalidResponse = Record.Exception(() => Replay(response.Response.RequestId,
            history[0], correction, response with { Response = response.Response with { SourceVersion = 1 } }));
        var invalidLink = Record.Exception(() => Replay(link.Link.RequestId,
            [.. history, link with { Link = link.Link with { Finding = finding with { TenantId = Uuid.CreateVersion4() } } }]));

        // Assert
        Assert.IsType<InvalidOperationException>(invalidCorrection);
        Assert.IsType<InvalidOperationException>(invalidResponse);
        Assert.IsType<InvalidOperationException>(invalidLink);
    }

    static AuditObservation New(Uuid requestId) => new(TenantId, ProgramId, ObservationId, requestId);

    static AuditObservation Recorded(AuditObservationContent content)
    {
        var requestId = Uuid.CreateVersion4();
        var observation = New(requestId);
        Assert.Null(observation.Record(requestId, content, ActorId, "Recorder", Now));
        return observation;
    }

    static AuditObservation Replay(Uuid requestId, params DomainEvent[] events) =>
        new AggregateScenario<AuditObservation>(New(requestId)).Given(events).Aggregate;

    static DomainEvent[] Events(AuditObservation observation, params DomainEvent[] prefix) =>
        [.. prefix, .. new AggregateScenario<AuditObservation>(observation).PendingEvents];

    static AuditObservationFindingReference Finding(AuditObservationContent content) =>
        new(TenantId, ProgramId, Uuid.CreateVersion4(), 1,
            new FindingSource("examination_item", ObservationId, "1", content.Wording));

    static AuditObservationContent Content() => new(Uuid.CreateVersion4(), "exception", "auditor",
        "auditor-message-1", "External auditor", Now, "Auditor wording", "medium",
        new ExaminationContentReference("type_ii_close", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('a', 64)),
        [new("control_occurrence", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('b', 64))]);
}
