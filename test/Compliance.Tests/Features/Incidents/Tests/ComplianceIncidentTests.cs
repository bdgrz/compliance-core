using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ComplianceIncidentAggregate = Bdgrz.Compliance.Features.Incidents.Rules.ComplianceIncidentRules;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Persistence;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Incidents.Tests;

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
    public async Task ShouldRetainCompleteChronologyGivenMoreThanThirtyTwoGovernedIncidentVersions()
    {
        // Arrange
        var incident = RecordedIncident();

        // Act
        for (var version = 2; version <= 40; version++)
            Assert.Null(incident.Revise(incident.Revision, Content() with { Title = "Governed incident correction " + version },
                "Correct incident narrative", RecorderId, "Compliance lead", Now.AddMinutes(version)));

        // Assert
        Assert.Equal(40, incident.Current!.CurrentVersionNumber);
        Assert.Equal(40, incident.Current.Versions.Count);
        await using var fixture = new IncidentNativeFixture();
        await IncidentSeeds.WriteAsync(fixture.Writer, fixture.Content, incident);
        var read = await fixture.Incidents.ReadAsync(TenantId, ProgramId, IncidentId);
        Assert.Equal(40, read.View!.Versions.Count);
        Assert.Equal(40, read.Owner.NativeSequence);
        var authority = await fixture.Content.ReadJsonAsync<ComplianceIncidentCurrentAuthority>(TenantId.ToString(), read.Owner.CurrentContent!);
        Assert.Empty(authority.Assessments);
        await foreach (var source in fixture.Events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString(), "compliance-incidents", IncidentId.ToString()), EventCursor.Start, CancellationToken.None))
            if (!source.Event.Metadata.IsAudit)
                Assert.True(Bdgrz.Compliance.Hosting.Persistence.DomainEventPayload.Size(source.Event) < Bdgrz.Compliance.Hosting.Persistence.DomainEventPayload.MaximumBytes);
    }

    [Fact]
    public async Task ShouldRetainAllGovernedReferencesGivenIncidentContentExceedsEventEnvelope()
    {
        // Arrange
        var incident = NewIncident();
        var content = Content() with
        {
            AffectedSystemIds = Enumerable.Range(0, 1000).Select(_ => Uuid.CreateVersion4()).ToArray(),
            EvidenceArtifactIds = Enumerable.Range(0, 1000).Select(_ => Uuid.CreateVersion4()).ToArray(),
        };

        // Act
        var failure = incident.Record(content, RecorderId, "Compliance lead", Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(1000, incident.Current!.Current!.Content.AffectedSystemIds.Count);
        Assert.Equal(1000, incident.Current.Current.Content.EvidenceArtifactIds.Count);
        await using var fixture = new IncidentNativeFixture();
        await IncidentSeeds.WriteAsync(fixture.Writer, fixture.Content, incident);
        var read = await fixture.Incidents.ReadAsync(TenantId, ProgramId, IncidentId);
        Assert.Equal(1000, read.View!.Current!.Content.AffectedSystemIds.Count);
        Assert.Equal(1000, read.View.Current.Content.EvidenceArtifactIds.Count);
    }

    [Fact]
    public async Task ShouldRejectOversizedConcreteEventGivenIndividuallyValidIncidentFields()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var incident = NewIncident();
        Assert.Null(incident.Record(Content(), RecorderId, "Compliance lead", Now));
        var owner = new Bdgrz.Compliance.Features.Incidents.Aggregates.ComplianceIncident(TenantId, ProgramId, IncidentId);
        var frozen = await Bdgrz.Compliance.Features.Incidents.Persistence.ComplianceIncidentContentStore.FreezeAsync(fixture.Content, null, null, null, incident,
            nameof(RecordComplianceIncident), CancellationToken.None);
        var op = Bdgrz.Compliance.Features.NativeOperations.Services.OwnerProvenance.For(owner, IncidentId, nameof(RecordComplianceIncident),
            Bdgrz.Compliance.Features.AccessControl.Models.ActorReference.ForMember(RecorderId, new string('x', 500000)), IncidentId.ToString(), null,
            new string('a', 64), Now) with
        { FirstOwner = owner.Stream };
        var response = await Bdgrz.Compliance.Features.NativeOperations.Services.NativeContent.FreezeUnitAsync(fixture.Content, TenantId);
        var first = await Bdgrz.Compliance.Features.NativeOperations.Services.FirstResponses.FreezeAsync(fixture.Content, TenantId.ToString(), response.Response);
        var fact = ComplianceIncidentContentStore.Fact(frozen, 1, op, first, response.Result);

        // Act
        var result = owner.Append(fact, frozen, 0, 0);

        // Assert
        Assert.True(Bdgrz.Compliance.Hosting.Persistence.DomainEventPayload.Size(fact) > Bdgrz.Compliance.Hosting.Persistence.DomainEventPayload.MaximumBytes);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<Bdgrz.Compliance.Features.Incidents.Aggregates.ComplianceIncident>(owner).PendingEvents);
    }

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
        Assert.Null(incident.RecordSystemDescriptionDecision("no_change",
            Uuid.CreateVersion4(), "The incident response did not change the approved system description",
            ResolverId, "Compliance lead", Now.AddHours(1).AddMinutes(30)));

        // Act
        var unresolved = incident.Resolve(incident.Revision, "Contained and customer records restored", Now.AddHours(2), [Uuid.CreateVersion4()], [], ResolverId, "Compliance lead", Now.AddHours(2));
        var resolved = incident.Resolve(incident.Revision, "Contained and customer records restored", Now.AddHours(2), [Uuid.CreateVersion4()], [workId], ResolverId, "Compliance lead", Now.AddHours(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(unresolved).Code);
        Assert.Null(resolved);
        Assert.Equal("resolved", incident.Current!.Status);
        Assert.Equal(workId, Assert.Single(incident.Current.Resolution!.ResolvedFollowUpWorkIds));
        Assert.Equal(workId, Assert.Single(incident.Current.Resolution.FollowUpCompletions).WorkItemId);
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
        Assert.Null(incident.RecordSystemDescriptionDecision("no_change",
            Uuid.CreateVersion4(), "The incident response did not change the approved system description",
            ResolverId, "Compliance lead", Now.AddHours(1).AddMinutes(30)));
        Assert.Null(incident.Resolve(incident.Revision, "Contained and customer records restored", Now.AddHours(2), [Uuid.CreateVersion4()], [], ResolverId, "Compliance lead", Now.AddHours(2)));
        var events = incident.AppliedChanges.ToArray();

        // Act
        var replay = NewIncident();
        foreach (var value in events)
            replay.Apply(value);

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
