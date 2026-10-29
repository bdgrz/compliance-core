using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests;

/// <summary>Pure-rule incident test conveniences; no native acceptance or production source proof is manufactured.</summary>
static class IncidentFacts
{
    /// <summary>A pure-rule fixture completion carrying a well-formed proof shape of a corrective-action completion.</summary>
    public static ComplianceIncidentFollowUpCompletionView Completion(ComplianceIncidentRules incident, Uuid workItemId) =>
        new(workItemId, new SourceFactProof<CorrectiveActionCompletedV2>(
            new EventStreamAddress(incident.Stream.Realm, CorrectiveAction.Area, Uuid.CreateVersion4().ToString()),
            Uuid.CreateVersion4(), 2, 3, EventKind.Of(typeof(CorrectiveActionCompletedV2)), new string('d', 64),
            new ImmutableContentRef(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json", "source-fact-locator.v1"), 30, 20));

    public static CommandFailure? Revise(this ComplianceIncidentRules incident, long expectedRevision,
        ComplianceIncidentContent content, string changeReason, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset at) => incident.Revise(expectedRevision, content, changeReason, actorMemberId, actorDisplay, at);

    public static CommandFailure? Assess(this ComplianceIncidentRules incident, long expectedRevision,
        ComplianceIncidentImpactAssessmentContent content, Uuid assessorMemberId, string assessorDisplay,
        DateTimeOffset at) => incident.Assess(expectedRevision, content, assessorMemberId, assessorDisplay, at);

    public static CommandFailure? RecordSystemDescriptionDecision(this ComplianceIncidentRules incident, string decision,
        Uuid decisionId, string rationale, Uuid actorMemberId, string actorDisplay, DateTimeOffset at) =>
        incident.RecordSystemDescriptionDecision(decision, decisionId, rationale, actorMemberId, actorDisplay, at);

    public static CommandFailure? Resolve(this ComplianceIncidentRules incident, long expectedRevision, string resolution,
        DateTimeOffset resolvedAt, IReadOnlyList<Uuid> evidenceArtifactIds, IReadOnlyList<Uuid> followUpWorkIds,
        Uuid resolverMemberId, string resolverDisplay, DateTimeOffset at) => incident.Resolve(expectedRevision,
        new VerifiedIncidentResolution(resolution, resolvedAt, evidenceArtifactIds,
            followUpWorkIds.Select(id => Completion(incident, id)).ToArray()), resolverMemberId, resolverDisplay, at);
}
