using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Features.Changes;

public sealed partial class SystemDescriptionDecisionReactor(
    IProjectionCheckpointStore checkpoints, IRequestBus bus, IAggregateReader reader,
    ISystemDescriptionDecisionCorrelationReader assessments)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("system-descriptions"), WorkloadName),
      IReactorHandler<SystemDescriptionApproved>,
      IReactorHandler<SystemDescriptionNoChangeRecorded>
{
    public const string WorkloadName = "SystemDescriptionImpactDecisionsV1";

    public async ValueTask HandleAsync(IReactorContext<SystemDescriptionApproved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        ValidateSource(context.Source.Stream, ev.TenantId, ev.ProgramId, ev.SystemDescriptionId);
        var description = await reader.HydrateAsync(new SystemDescriptionAggregate(ev.TenantId,
            ev.ProgramId, ev.SystemDescriptionId), ct).ConfigureAwait(false);
        var version = description.FindVersion(ev.Approval.VersionId);
        if (version is null || version.Status != "approved" ||
            description.FindApproval(ev.Approval.VersionId) != ev.Approval ||
            version.Version != ev.Approval.Version || version.TenantId != ev.TenantId ||
            version.ProgramId != ev.ProgramId)
            throw new InvalidOperationException(
                "An approved description reaction must use the exact committed approval and version.");

        foreach (var changeId in version.Content.SignificantChangeIds.OrderBy(
                     static id => id.ToString(), StringComparer.Ordinal))
        {
            var assessment = await assessments.GetCurrentChangeAsync(ev.TenantId,
                ev.ProgramId, changeId, ct).ConfigureAwait(false);
            if (assessment is not { RequiresSystemDescriptionReview: true })
                continue;
            var correlated = SystemDescriptionDecisionCorrelation.Find(description, ev.TenantId,
                ev.ProgramId, changeId, version.SystemDescriptionVersionId,
                version.SystemDescriptionVersionId, "approved_successor");
            if (correlated is null)
                throw new InvalidOperationException(
                    "An approved description does not retain an exact change correlation.");
            if (correlated.DecidedAt < assessment.AssessedAt)
                continue;
            await ImpactReactionOutcomes.SendAsync(bus, new RecordSignificantChangeSystemDescriptionDecision(
                EffectIdentity.CommandId(ev.Metadata.EventId, "change:system-description-decision", $"{ev.TenantId}:{changeId}", null),
                ev.TenantId, ev.ProgramId, changeId, assessment.ContentVersion,
                ev.SystemDescriptionId, version.SystemDescriptionVersionId,
                correlated.DecisionId, correlated.Decision, assessment.AssessmentSha256),
                context, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask HandleAsync(IReactorContext<SystemDescriptionNoChangeRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        ValidateSource(context.Source.Stream, ev.TenantId, ev.ProgramId, ev.SystemDescriptionId);
        var description = await reader.HydrateAsync(new SystemDescriptionAggregate(ev.TenantId,
            ev.ProgramId, ev.SystemDescriptionId), ct).ConfigureAwait(false);
        if (description.FindVersion(ev.Decision.VersionId) is not { } version ||
            description.NoChangeDecisions.FirstOrDefault(item =>
                item.VersionId == ev.Decision.VersionId && item.ChangeId == ev.Decision.ChangeId)
                != ev.Decision ||
            version.Content.PeriodChanges.FirstOrDefault(change =>
                change.ChangeId == ev.Decision.ChangeId) is not { IsMaterial: false })
            throw new InvalidOperationException(
                "A no-change reaction must use the exact committed period-change decision.");

        var change = await reader.HydrateAsync(new SignificantChange(ev.TenantId,
            ev.ProgramId, ev.Decision.ChangeId), ct).ConfigureAwait(false);
        var incident = await reader.HydrateAsync(new ComplianceIncident(ev.TenantId,
            ev.ProgramId, ev.Decision.ChangeId), ct).ConfigureAwait(false);
        if (change.Revision > 0 && incident.Revision > 0)
            throw new InvalidOperationException(
                "A no-change decision ambiguously identifies both a change and an incident.");

        var changeAssessment = await assessments.GetCurrentChangeAsync(ev.TenantId,
            ev.ProgramId, ev.Decision.ChangeId, ct).ConfigureAwait(false);
        var incidentAssessment = await assessments.GetCurrentIncidentAsync(ev.TenantId,
            ev.ProgramId, ev.Decision.ChangeId, ct).ConfigureAwait(false);

        var decisionId = SystemDescriptionDecisionCorrelation.NoChangeDecisionId(
            ev.Decision.VersionId, ev.Decision.ChangeId);
        if (changeAssessment is { RequiresSystemDescriptionReview: true } currentChangeAssessment)
        {
            var correlated = SystemDescriptionDecisionCorrelation.Find(description, ev.TenantId,
                ev.ProgramId, ev.Decision.ChangeId, ev.Decision.VersionId, decisionId, "no_change",
                currentChangeAssessment.AssessmentSha256);
            if (correlated is null)
                throw new InvalidOperationException(
                    "A no-change decision does not retain an exact change correlation.");
            await ImpactReactionOutcomes.SendAsync(bus, new RecordSignificantChangeSystemDescriptionDecision(
                EffectIdentity.CommandId(ev.Metadata.EventId, "change:system-description-decision", $"{ev.TenantId}:{ev.Decision.ChangeId}", null),
                ev.TenantId, ev.ProgramId, ev.Decision.ChangeId,
                currentChangeAssessment.ContentVersion, ev.SystemDescriptionId,
                ev.Decision.VersionId, decisionId, "no_change",
                currentChangeAssessment.AssessmentSha256), context, ct)
                .ConfigureAwait(false);
            return;
        }

        if (incidentAssessment is { RequiresSystemDescriptionReview: true } currentIncidentAssessment)
        {
            var correlated = SystemDescriptionDecisionCorrelation.Find(description, ev.TenantId,
                ev.ProgramId, ev.Decision.ChangeId, ev.Decision.VersionId, decisionId, "no_change",
                currentIncidentAssessment.AssessmentSha256);
            if (correlated is null)
                throw new InvalidOperationException(
                    "A no-change decision does not retain an exact incident correlation.");
            await ImpactReactionOutcomes.SendAsync(bus, new RecordComplianceIncidentSystemDescriptionDecision(
                EffectIdentity.CommandId(ev.Metadata.EventId, "incident:system-description-decision",
                    $"{ev.TenantId}:{ev.Decision.ChangeId}", null),
                ev.TenantId, ev.ProgramId, ev.Decision.ChangeId,
                currentIncidentAssessment.ContentVersion, ev.SystemDescriptionId,
                ev.Decision.VersionId, decisionId, currentIncidentAssessment.AssessmentSha256),
                context, ct).ConfigureAwait(false);
        }
    }

    void ValidateSource(EventStreamAddress source, Uuid tenantId, Uuid programId,
        Uuid descriptionId)
    {
        if (Pattern.IsTenantTemplate || Pattern.Realm != tenantId.ToString() ||
            source != new SystemDescriptionAggregate(tenantId, programId, descriptionId).Stream)
            throw new InvalidOperationException(
                "A system-description decision reaction must originate from its tenant-owned aggregate.");
    }
}
