using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

/// <summary>Accepts incident operations on the aggregate under fresh logical ids for tests that need a state.</summary>
static class IncidentFacts
{
    static readonly ActorReference Actor = ActorReference.ForSystemProcess("system", "system");

    /// <summary>The provenance of one accepted incident operation under a logical id.</summary>
    public static OperationProvenance Provenance(ComplianceIncident incident, string commandKind, DateTimeOffset at,
        Uuid commandId = default, ActorReference? actor = null)
    {
        var id = commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId;
        var owner = incident.Stream;
        return new OperationProvenance(id, id, null, commandKind, actor ?? Actor, owner, "incident", null,
            new string('a', 64), at, owner, owner);
    }

    /// <summary>A frozen first-response reference of the right schema; nothing reads it back.</summary>
    public static ImmutableContentRef Response() => new(Uuid.CreateVersion4(), new string('b', 64), 2,
        "application/json", "first-response-descriptor.v1");

    /// <summary>A verified completion carrying a well-formed proof of a corrective-action completion.</summary>
    public static ComplianceIncidentFollowUpCompletionView Completion(ComplianceIncident incident, Uuid workItemId) =>
        new(workItemId, new SourceFactProof<CorrectiveActionCompletedV2>(
            new EventStreamAddress(incident.Stream.Realm, FindingAggregate.Area, Uuid.CreateVersion4().ToString()),
            Uuid.CreateVersion4(), 2, 3, EventKind.Of(typeof(CorrectiveActionCompletedV2)), new string('d', 64),
            Response(), 30, 20));

    public static CommandFailure? Revise(this ComplianceIncident incident, long expectedRevision,
        ComplianceIncidentContent content, string changeReason, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset at) => incident.Revise(Provenance(incident, "ReviseComplianceIncident", at), Response(),
        expectedRevision, content, changeReason, actorMemberId, actorDisplay);

    public static CommandFailure? Assess(this ComplianceIncident incident, long expectedRevision,
        ComplianceIncidentImpactAssessmentContent content, Uuid assessorMemberId, string assessorDisplay,
        DateTimeOffset at) => incident.Assess(Provenance(incident, "AssessComplianceIncident", at), Response(),
        expectedRevision, content, assessorMemberId, assessorDisplay);

    public static CommandFailure? RecordSystemDescriptionDecision(this ComplianceIncident incident, string decision,
        Uuid decisionId, string rationale, Uuid actorMemberId, string actorDisplay, DateTimeOffset at) =>
        incident.RecordSystemDescriptionDecision(Provenance(incident, "RecordComplianceIncidentSystemDescriptionDecision",
            at), Response(), decision, decisionId, rationale, actorMemberId, actorDisplay, at);

    public static CommandFailure? Resolve(this ComplianceIncident incident, long expectedRevision, string resolution,
        DateTimeOffset resolvedAt, IReadOnlyList<Uuid> evidenceArtifactIds, IReadOnlyList<Uuid> followUpWorkIds,
        Uuid resolverMemberId, string resolverDisplay, DateTimeOffset at) => incident.Resolve(
        Provenance(incident, "RecordComplianceIncidentResolution", at), Response(), expectedRevision,
        new VerifiedIncidentResolution(resolution, resolvedAt, evidenceArtifactIds,
            followUpWorkIds.Select(id => Completion(incident, id)).ToArray()), resolverMemberId, resolverDisplay);
}
