using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Events;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.Aggregates.SystemDescription;

namespace Bdgrz.Compliance.Features.Changes.Reactors;

public sealed partial class SystemDescriptionDecisionReactor(
    IProjectionCheckpointStore checkpoints, IRequestBus bus, IAggregateReader reader,
    SystemDescriptionReader descriptions, IImmutableContentStore content,
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
        var description = await descriptions.ReadAsync(ev.TenantId, ev.ProgramId, ev.SystemDescriptionId, ct).ConfigureAwait(false);
        RequireSource(description, context.Source.Stream, ev);
        if (ev.ChangeCount == 0)
            return;
        var approval = ((SystemDescriptionApprovedBody)await SystemDescriptionContent.ReadMutationAsync(content, ev, ct).ConfigureAwait(false)).Approval;
        var selected = await descriptions.VersionSourceAsync(ev.TenantId, ev.ProgramId, ev.SystemDescriptionId, approval.VersionId, ct).ConfigureAwait(false);
        var version = selected?.Version;
        if (selected is null || selected.ApprovalSource != NativeFactReference.Of(description.Stream, ev) || selected.Approval != approval || version is null || version.Status != "approved" ||
            description.FindApproval(approval.VersionId) != approval ||
            version.Version != approval.Version || version.TenantId != ev.TenantId ||
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
        var description = await descriptions.ReadAsync(ev.TenantId, ev.ProgramId, ev.SystemDescriptionId, ct).ConfigureAwait(false);
        RequireSource(description, context.Source.Stream, ev);
        if (ev.ChangeCount == 0)
            return;
        var decision = ((SystemDescriptionNoChangeRecordedBody)await SystemDescriptionContent.ReadMutationAsync(content, ev, ct).ConfigureAwait(false)).Decision;
        if (description.FindVersion(decision.VersionId) is not { } version ||
            description.NoChangeDecisions.FirstOrDefault(item =>
                item.VersionId == decision.VersionId && item.ChangeId == decision.ChangeId)
                != decision ||
            version.Content.PeriodChanges.FirstOrDefault(change =>
                change.ChangeId == decision.ChangeId) is not { IsMaterial: false })
            throw new InvalidOperationException(
                "A no-change reaction must use the exact committed period-change decision.");

        var change = await reader.HydrateAsync(new SignificantChange(ev.TenantId,
            ev.ProgramId, decision.ChangeId), ct).ConfigureAwait(false);
        var incident = await reader.HydrateAsync(new ComplianceIncident(ev.TenantId,
            ev.ProgramId, decision.ChangeId), ct).ConfigureAwait(false);
        if (change.Revision > 0 && incident.Header?.ProgramId == ev.ProgramId)
            throw new InvalidOperationException(
                "A no-change decision ambiguously identifies both a change and an incident.");

        var changeAssessment = await assessments.GetCurrentChangeAsync(ev.TenantId,
            ev.ProgramId, decision.ChangeId, ct).ConfigureAwait(false);
        var incidentAssessment = await assessments.GetCurrentIncidentAsync(ev.TenantId,
            ev.ProgramId, decision.ChangeId, ct).ConfigureAwait(false);

        var decisionId = SystemDescriptionDecisionCorrelation.NoChangeDecisionId(
            decision.VersionId, decision.ChangeId);
        if (changeAssessment is { RequiresSystemDescriptionReview: true } currentChangeAssessment)
        {
            var correlated = SystemDescriptionDecisionCorrelation.Find(description, ev.TenantId,
                ev.ProgramId, decision.ChangeId, decision.VersionId, decisionId, "no_change",
                currentChangeAssessment.AssessmentSha256);
            if (correlated is null)
                throw new InvalidOperationException(
                    "A no-change decision does not retain an exact change correlation.");
            await ImpactReactionOutcomes.SendAsync(bus, new RecordSignificantChangeSystemDescriptionDecision(
                EffectIdentity.CommandId(ev.Metadata.EventId, "change:system-description-decision", $"{ev.TenantId}:{decision.ChangeId}", null),
                ev.TenantId, ev.ProgramId, decision.ChangeId,
                currentChangeAssessment.ContentVersion, ev.SystemDescriptionId,
                decision.VersionId, decisionId, "no_change",
                currentChangeAssessment.AssessmentSha256), context, ct)
                .ConfigureAwait(false);
            return;
        }

        if (incidentAssessment is { RequiresSystemDescriptionReview: true } currentIncidentAssessment)
        {
            var correlated = SystemDescriptionDecisionCorrelation.Find(description, ev.TenantId,
                ev.ProgramId, decision.ChangeId, decision.VersionId, decisionId, "no_change",
                currentIncidentAssessment.AssessmentSha256);
            if (correlated is null)
                throw new InvalidOperationException(
                    "A no-change decision does not retain an exact incident correlation.");
            await ImpactReactionOutcomes.SendAsync(bus, new RecordComplianceIncidentSystemDescriptionDecision(
                EffectIdentity.CommandId(ev.Metadata.EventId, "incident:system-description-decision",
                    $"{ev.TenantId}:{decision.ChangeId}", null),
                ev.TenantId, ev.ProgramId, decision.ChangeId,
                currentIncidentAssessment.ContentVersion, ev.SystemDescriptionId,
                decision.VersionId, decisionId, currentIncidentAssessment.AssessmentSha256),
                context, ct).ConfigureAwait(false);
        }
    }

    static void RequireSource(SystemDescriptionReadSnapshot description, EventStreamAddress stream, DomainEvent source)
    {
        var actual = description.Facts.SingleOrDefault(fact => fact.Metadata.EventId == source.Metadata.EventId);
        if (actual is null)
            throw new ImmutableContentUnavailableException("The exact system-description decision source is not visible yet.");
        if (actual.GetType() != source.GetType() || NativeFactReference.Of(stream, actual) != NativeFactReference.Of(stream, source) ||
            ContentDigest.Sha256(actual, actual.GetType()) != ContentDigest.Sha256(source, source.GetType()))
            throw new ImmutableContentIntegrityException("The decision reaction differs from its exact native source fact.");
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
