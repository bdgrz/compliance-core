using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>
///     The ordered engagement acceptance and staff assignment process. Acquire walks one global canonical stream key
///     order from the frozen manifests: every key is offered to every receiver in turn, and the receiver that owns the
///     key (the service epoch, a source use or a practice subject) acquires it in its final mode, with no upgrades and
///     no key waiting for another. Authorize is the irreversible commit point that requires the complete exact grant
///     set; commit then makes every permanent wall, complete records the acceptance or assignment, and settle ends
///     only the temporary grants. A receiver that proves the operation can never proceed rejects it: the abort
///     receiver then fences it before authorization, and settle fences every key, held or not. Permanent commitments
///     are never released by any stage.
/// </summary>
static class EngagementAcceptanceProcess
{
    public const string Acquire = "acquire";
    public const string Authorize = "authorize";
    public const string Commit = "commit";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(EngagementAcceptanceEffects.ProcessName,
        [nameof(ServiceEngagementAcceptanceRequested), nameof(ServiceEngagementStaffAssignmentRequested)], true,
        [
            new ProcessStageDefinition(Acquire,
            [
                nameof(AcquireEngagementServiceEpoch),
                nameof(AuthorizeFirmStaffRecordEngagementUse),
                nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse),
                nameof(AuthorizeIndependenceRuleCatalogEngagementUse),
                nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse),
                nameof(ReservePracticeIndependence),
            ], "One process acquires every key from the frozen manifest in canonical unsigned UTF8 stream-key order, in final mode, with no upgrades. The receiver list is not an acquisition order; the manifest is.")
            {
                Targeting = StageTargeting.Interleaved,
            },
            new ProcessStageDefinition(Authorize, [nameof(AuthorizeEngagementPracticeCommitments)],
                "Irreversible commit point; requires the complete exact grant set.")
            {
                Targeting = StageTargeting.Once,
            },
            new ProcessStageDefinition(Commit, [nameof(CommitPracticeIndependence)],
                "Permanent walls; survive every later outcome.")
            {
                Targeting = StageTargeting.EachReceiver,
            },
            new ProcessStageDefinition(Complete,
            [
                nameof(CompleteServiceEngagementAcceptance),
                nameof(CompleteServiceEngagementStaffAssignment),
            ])
            {
                Targeting = StageTargeting.Once,
            },
            new ProcessStageDefinition(Settle,
            [
                nameof(ReleaseEngagementServiceEpoch),
                nameof(SettleFirmStaffRecordEngagementUse),
                nameof(SettleFirmProfessionalDutyDesignationEngagementUse),
                nameof(SettleIndependenceRuleCatalogEngagementUse),
                nameof(SettleIndependenceRuleRatificationCatalogEngagementUse),
                nameof(SettlePracticeIndependenceReservation),
            ], "Temporary grants only; permanent commitments remain.")
            {
                Targeting = StageTargeting.EachReceiver,
            },
        ], [nameof(RejectServiceEngagementAcceptance), nameof(RejectServiceEngagementStaffAssignment)],
        CreateReceiverAsync);

    /// <summary>The canonical acquisition keys of a frozen intent: every source use and every practice subject.</summary>
    public static IReadOnlyList<string> Targets(IEnumerable<SourceUseRequirement> requirements,
        IEnumerable<PracticeSubjectIntent> subjects) => requirements
        .Select(requirement => EngagementAcceptanceEffects.StreamKey(requirement.SourceOwner))
        .Concat(subjects.Select(subject => subject.Key)).Distinct(StringComparer.Ordinal)
        .Order(Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance).ToArray();

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        var operationId = record.Key.RootOperationId;
        var engagement = record.Source.Stream;
        var engagementId = Uuid.Parse(engagement.Resource, CultureInfo.InvariantCulture);

        EngagementIndependenceIntent intent;
        IReadOnlyList<SourceUseRequirement> requirements;
        IReadOnlyList<PracticeSubjectIntent> subjects;
        try
        {
            intent = await content.ReadJsonAsync<EngagementIndependenceIntent>(record.Key.Realm, record.Intent, ct)
                .ConfigureAwait(false);
            requirements = await content.ReadSelectionAsync<SourceUseRequirement>(record.Key.Realm, intent.SourceUses,
                ct).ConfigureAwait(false);
            subjects = await content.ReadSelectionAsync<PracticeSubjectIntent>(record.Key.Realm, intent.Subjects, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen acceptance is not readable yet: {exception.Message}");
        }

        var aborting = record.State == ProcessState.Aborting;
        var disposition = aborting
            ? SourceUseSettlementDisposition.Canceled
            : SourceUseSettlementDisposition.IrreversibleDecision;
        var generation = EngagementAcceptanceEffects.Generation;
        var assignment = intent.OperationKind == EngagementIndependenceOperationKind.StaffAssignment;
        var requirement = step.Target is null
            ? null
            : requirements.SingleOrDefault(item => EngagementAcceptanceEffects.StreamKey(item.SourceOwner) == step.Target);
        var subject = step.Target is null ? null : subjects.SingleOrDefault(item => item.Key == step.Target);
        var area = requirement?.SourceOwner.Area;

        switch (step.Receiver)
        {
            case nameof(AcquireEngagementServiceEpoch) when area == ClientServiceIndependence.Area:
                return new AcquireEngagementServiceEpoch(step.CommandId, record.Source, requirement!.SourceOwner,
                    operationId, generation, null, record.Intent, engagementId, (long)intent.ServiceEpoch);
            case nameof(AuthorizeFirmStaffRecordEngagementUse) when area == FirmStaffRecord.Area:
                return new AuthorizeFirmStaffRecordEngagementUse(step.CommandId, record.Source,
                    requirement!.SourceOwner, operationId, generation, null, record.Intent,
                    Uuid.Parse(requirement.SourceOwner.Resource, CultureInfo.InvariantCulture),
                    requirement.ContentEvent.EventId, requirement.ResourceRevision, engagementId);
            case nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse)
                when area == FirmProfessionalDutyDesignation.Area:
                return new AuthorizeFirmProfessionalDutyDesignationEngagementUse(step.CommandId, record.Source,
                    requirement!.SourceOwner, operationId, generation, null, record.Intent,
                    Uuid.Parse(intent.Header.OriginalActor.Id, CultureInfo.InvariantCulture),
                    requirement.ContentEvent.EventId, requirement.ResourceRevision, engagementId);
            case nameof(AuthorizeIndependenceRuleCatalogEngagementUse) when area == IndependenceRuleCatalog.Area:
                return new AuthorizeIndependenceRuleCatalogEngagementUse(step.CommandId, record.Source,
                    requirement!.SourceOwner, operationId, generation, null, record.Intent,
                    IndependenceRuleCatalog.Identity, requirement.ContentEvent.EventId, requirement.ResourceRevision,
                    engagementId);
            case nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse)
                when area == IndependenceRuleRatificationCatalog.Area:
                return new AuthorizeIndependenceRuleRatificationCatalogEngagementUse(step.CommandId, record.Source,
                    requirement!.SourceOwner, operationId, generation, null, record.Intent,
                    IndependenceRuleRatificationCatalog.Identity, requirement.ContentEvent.EventId,
                    requirement.ResourceRevision, engagementId);
            case nameof(ReservePracticeIndependence) when subject is not null:
                return new ReservePracticeIndependence(step.CommandId, record.Source, subject.Stream, operationId,
                    generation, null, record.Intent, subject.Kind, subject.SubjectId, subject.Practice);
            case nameof(AuthorizeEngagementPracticeCommitments):
                return new AuthorizeEngagementPracticeCommitments(step.CommandId, record.Source, engagement,
                    operationId, generation, null, record.Intent,
                    await EngagementAcceptanceProofs.ReservationsAsync(reader, content, record.Source, intent,
                        operationId, subjects, ct).ConfigureAwait(false),
                    await EngagementAcceptanceProofs.SourceUsesAsync(reader, content, record.Source, intent,
                        operationId, requirements, ct).ConfigureAwait(false));
            case nameof(CommitPracticeIndependence) when subject is not null:
                return new CommitPracticeIndependence(step.CommandId, AuthorizedFact(record), subject.Stream,
                    operationId, generation, null, record.Intent, subject.Kind, subject.SubjectId, subject.Practice);
            case nameof(CompleteServiceEngagementAcceptance) when !assignment:
                return new CompleteServiceEngagementAcceptance(step.CommandId, AuthorizedFact(record), engagement,
                    operationId, generation, null, record.Intent, engagementId, (long)intent.ServiceEpoch,
                    await EngagementAcceptanceProofs.CommitmentsAsync(reader, content, record.Source, intent,
                        operationId, subjects, ct).ConfigureAwait(false));
            case nameof(CompleteServiceEngagementStaffAssignment) when assignment:
                return new CompleteServiceEngagementStaffAssignment(step.CommandId, AuthorizedFact(record), engagement,
                    operationId, generation, null, record.Intent, engagementId, (long)intent.ServiceEpoch,
                    await EngagementAcceptanceProofs.CommitmentsAsync(reader, content, record.Source, intent,
                        operationId, subjects, ct).ConfigureAwait(false),
                    intent.StaffMemberId ?? throw new SourceFactVerificationException(
                        "The frozen staff assignment names no staff member."));
            case nameof(ReleaseEngagementServiceEpoch) when area == ClientServiceIndependence.Area:
                return new ReleaseEngagementServiceEpoch(step.CommandId, TerminalFact(record, assignment, aborting),
                    requirement!.SourceOwner, operationId, generation, null, record.Intent, engagementId,
                    (long)intent.ServiceEpoch, disposition);
            case nameof(SettleFirmStaffRecordEngagementUse) when area == FirmStaffRecord.Area:
                return new SettleFirmStaffRecordEngagementUse(step.CommandId, TerminalFact(record, assignment, aborting),
                    requirement!.SourceOwner, operationId, generation, null, record.Intent,
                    Uuid.Parse(requirement.SourceOwner.Resource, CultureInfo.InvariantCulture), engagementId,
                    disposition);
            case nameof(SettleFirmProfessionalDutyDesignationEngagementUse)
                when area == FirmProfessionalDutyDesignation.Area:
                return new SettleFirmProfessionalDutyDesignationEngagementUse(step.CommandId,
                    TerminalFact(record, assignment, aborting), requirement!.SourceOwner, operationId, generation, null,
                    record.Intent, Uuid.Parse(intent.Header.OriginalActor.Id, CultureInfo.InvariantCulture),
                    engagementId, disposition);
            case nameof(SettleIndependenceRuleCatalogEngagementUse) when area == IndependenceRuleCatalog.Area:
                return new SettleIndependenceRuleCatalogEngagementUse(step.CommandId,
                    TerminalFact(record, assignment, aborting), requirement!.SourceOwner, operationId, generation, null,
                    record.Intent, IndependenceRuleCatalog.Identity, engagementId, disposition);
            case nameof(SettleIndependenceRuleRatificationCatalogEngagementUse)
                when area == IndependenceRuleRatificationCatalog.Area:
                return new SettleIndependenceRuleRatificationCatalogEngagementUse(step.CommandId,
                    TerminalFact(record, assignment, aborting), requirement!.SourceOwner, operationId, generation, null,
                    record.Intent, IndependenceRuleRatificationCatalog.Identity, engagementId, disposition);
            case nameof(SettlePracticeIndependenceReservation) when subject is not null:
                return new SettlePracticeIndependenceReservation(step.CommandId,
                    TerminalFact(record, assignment, aborting), subject.Stream, operationId, generation, null,
                    record.Intent, subject.Kind, subject.SubjectId, disposition);
            case nameof(RejectServiceEngagementAcceptance) when !assignment:
                return new RejectServiceEngagementAcceptance(step.CommandId, operationId, engagement, generation, 0,
                    record.Intent);
            case nameof(RejectServiceEngagementStaffAssignment) when assignment:
                return new RejectServiceEngagementStaffAssignment(step.CommandId, operationId, engagement, generation,
                    0, record.Intent);
            default:
                return null;
        }
    }

    static NativeFactReference AuthorizedFact(ProcessRecord record) =>
        record.Facts.TryGetValue(nameof(AuthorizeEngagementPracticeCommitments), out var fact)
            ? fact
            : throw new SourceFactVerificationException("The authorization is not recorded in the process yet.");

    static NativeFactReference TerminalFact(ProcessRecord record, bool assignment, bool aborting)
    {
        var receiver = aborting
            ? assignment ? nameof(RejectServiceEngagementStaffAssignment) : nameof(RejectServiceEngagementAcceptance)
            : assignment
                ? nameof(CompleteServiceEngagementStaffAssignment)
                : nameof(CompleteServiceEngagementAcceptance);
        return record.Facts.TryGetValue(receiver, out var fact)
            ? fact
            : throw new SourceFactVerificationException("The terminal fact is not recorded in the process yet.");
    }
}
