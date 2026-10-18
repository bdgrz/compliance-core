using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ComplianceIncidentAggregate = Bdgrz.Compliance.Features.Incidents.ComplianceIncident;

namespace Bdgrz.Compliance.Tests.Features.Incidents;

public sealed class ComplianceIncidentTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid IncidentId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid RecorderId = Uuid.CreateVersion4();
    static readonly Uuid AssessorId = Uuid.CreateVersion4();
    static readonly Uuid ResolverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordNativeIncidentWithoutInventingSourceReferenceGivenManualCapture()
    {
        // Arrange
        var incident = NewIncident();

        // Act
        var failure = incident.Record(Content(), RecorderId, "Compliance lead", Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal("recorded", incident.Current!.Status);
        Assert.Null(incident.Current.Current!.Content.SourceSystem);
        Assert.Null(incident.Current.Current.Content.SourceIdentifier);
        Assert.Equal("confidential", incident.Current.Current.Content.HandlingClass);
        Assert.Equal(OwnerId, incident.Current.Current.Content.OwnerMemberId);
    }

    [Fact]
    public void ShouldApplyAcceptedIncidentSignificanceRuleGivenAssessment()
    {
        // Arrange
        var incident = RecordedIncident();

        // Act
        var unsupported = incident.Assess(incident.Revision, Assessment("significant",
            customerDataAffected: false, availabilitySlaAffected: false), AssessorId,
            "Compliance lead", Now.AddHours(1));
        var incompleteAssessment = Assessment("significant", customerDataAffected: true,
            availabilitySlaAffected: false) with
        {
            ScopeImpactReviewed = false,
        };
        var missingScopeReview = incident.Assess(incident.Revision, incompleteAssessment,
            AssessorId, "Compliance lead", Now.AddHours(1));
        var assessed = incident.Assess(incident.Revision, Assessment("significant",
            customerDataAffected: true, availabilitySlaAffected: false), AssessorId,
            "Compliance lead", Now.AddHours(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(unsupported).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(missingScopeReview).Code);
        Assert.Null(assessed);
        Assert.Equal("significant", incident.Current!.CurrentAssessment!.Significance);
        Assert.False(incident.Current.PeriodCloseEligible);
    }

    [Fact]
    public void ShouldRequireReviewedScopeAndControlsBeforeIncidentResolutionGivenMaterialIncident()
    {
        // Arrange
        var incident = RecordedIncident();
        var workId = Uuid.CreateVersion4();
        Assert.Null(incident.Assess(incident.Revision, Assessment("significant",
            customerDataAffected: true, availabilitySlaAffected: false, followUpWorkIds: [workId]),
            AssessorId, "Compliance lead", Now.AddHours(1)));
        Assert.Null(incident.RecordSystemDescriptionDecision(incident.Revision, "no_change",
            Uuid.CreateVersion4(), "The incident response did not change the approved system description",
            ResolverId, "Compliance lead", Now.AddHours(1).AddMinutes(30)));

        // Act
        var unresolved = incident.Resolve(incident.Revision, new ComplianceIncidentResolutionContent(
            "Contained and customer records restored", Now.AddHours(2), [Uuid.CreateVersion4()], []),
            ResolverId, "Compliance lead", Now.AddHours(2));
        var resolved = incident.Resolve(incident.Revision, new ComplianceIncidentResolutionContent(
            "Contained and customer records restored", Now.AddHours(2), [Uuid.CreateVersion4()], [workId]),
            ResolverId, "Compliance lead", Now.AddHours(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(unresolved).Code);
        Assert.Null(resolved);
        Assert.Equal("resolved", incident.Current!.Status);
        Assert.Equal(workId, Assert.Single(incident.Current.Resolution!.ResolvedFollowUpWorkIds));
        Assert.True(incident.Current.PeriodCloseEligible);
    }

    [Fact]
    public void ShouldReplayIncidentAssessmentAndResolutionGivenEvents()
    {
        // Arrange
        var incident = RecordedIncident();
        Assert.Null(incident.Assess(incident.Revision, Assessment("significant",
            customerDataAffected: true, availabilitySlaAffected: false), AssessorId,
            "Compliance lead", Now.AddHours(1)));
        Assert.Null(incident.RecordSystemDescriptionDecision(incident.Revision, "no_change",
            Uuid.CreateVersion4(), "The incident response did not change the approved system description",
            ResolverId, "Compliance lead", Now.AddHours(1).AddMinutes(30)));
        Assert.Null(incident.Resolve(incident.Revision, new ComplianceIncidentResolutionContent(
            "Contained and customer records restored", Now.AddHours(2), [Uuid.CreateVersion4()], []),
            ResolverId, "Compliance lead", Now.AddHours(2)));
        var events = new AggregateScenario<ComplianceIncidentAggregate>(incident).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ComplianceIncidentAggregate>(NewIncident()).Given(events).Aggregate;

        // Assert
        Assert.Equal("resolved", replay.Current!.Status);
        Assert.Equal(incident.Revision, replay.Current.Revision);
        Assert.Equal(incident.Current!.CurrentAssessment!.Significance,
            replay.Current.CurrentAssessment!.Significance);
        Assert.Equal(incident.Current.Resolution!.Resolution, replay.Current.Resolution!.Resolution);
    }

    static ComplianceIncidentAggregate NewIncident() => new(TenantId, ProgramId, IncidentId);

    static ComplianceIncidentAggregate RecordedIncident()
    {
        var incident = NewIncident();
        Assert.Null(incident.Record(Content(), RecorderId, "Compliance lead", Now));
        return incident;
    }

    static ComplianceIncidentContent Content() => new(null, null, null, null,
        "Payment API credential exposed", "A credential was briefly exposed and revoked",
        "security", "high", Now.AddMinutes(-30), Now, OwnerId, true, false,
        [Uuid.CreateVersion4()], "confidential", [Uuid.CreateVersion4()]);

    static ComplianceIncidentImpactAssessmentContent Assessment(string significance,
        bool customerDataAffected, bool availabilitySlaAffected,
        IReadOnlyList<Uuid> followUpWorkIds = default!) => new(significance,
        "Reviewed scope, controls, risk, evidence, and system description",
        true, true, true, true, ["scope", "control", "risk", "vendor", "evidence", "system_description"],
        [AffectedRecord("control"), AffectedRecord("system_description_section")],
        followUpWorkIds ?? [], [Uuid.CreateVersion4()], true, customerDataAffected,
        availabilitySlaAffected);

    static ComplianceIncidentAffectedRecordReference AffectedRecord(string recordType) => new(
        recordType, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1);
}
