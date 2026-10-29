using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.History;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
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
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;

/// <summary>Reads the exact admitted contribution and full immutable thread history at a native owner cut.</summary>
public sealed class AdvisorFeedbackReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    public async ValueTask<AdvisorFeedbackThreadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid handoffId,
        Uuid feedbackId, Uuid selectedLogicalId = default, CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId,
            selectedLogicalId), ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    public async ValueTask<AdvisorFeedbackThreadSnapshot> ReadAsync(AdvisorFeedbackThread owner,
        CancellationToken ct = default)
    {
        var facts = await AdvisorNativeHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        AdvisorFeedbackRecordedContent? recorded = null;
        var corrections = new List<AdvisorFeedbackCorrectionView>();
        var responses = new List<AdvisorFeedbackResponseView>();
        foreach (var fact in facts)
            switch (fact)
            {
                case AdvisorFeedbackRecorded value:
                    recorded = await ReadRecordedAsync(content, value, ct).ConfigureAwait(false);
                    break;
                case AdvisorFeedbackCorrected value:
                    var correction = await AdvisorAuthored.ReadAsync(content, value, ct).ConfigureAwait(false);
                    corrections.Add(new AdvisorFeedbackCorrectionView(value.CorrectionId, correction.CorrectedBody,
                        correction.CorrectionReason, value.CorrectedBy, value.CorrectedAt));
                    break;
                case AdvisorFeedbackResponded value:
                    var response = await AdvisorAuthored.ReadAsync(content, value, ct).ConfigureAwait(false);
                    responses.Add(new AdvisorFeedbackResponseView(response.Response, value.RespondedBy, value.RespondedAt));
                    break;
            }
        return new AdvisorFeedbackThreadSnapshot(owner, facts, recorded, corrections, responses);
    }

    internal async ValueTask<bool> HasAdmissionAsync(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        CancellationToken ct)
    {
        var parent = await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId, handoffId, feedbackId), ct)
            .ConfigureAwait(false);
        _ = await AdvisorNativeHistory.ReadAsync(parent, events, ct).ConfigureAwait(false);
        return AdvisorHandoffRules.InProgram(parent, programId) &&
            parent.Acceptance.SelectedFact is AdvisorFeedbackAdmitted admission && admission.Operation.LogicalId == feedbackId;
    }

    internal static async Task<AdvisorFeedbackRecordedContent> ReadRecordedAsync(IImmutableContentStore content,
        AdvisorFeedbackRecorded fact, CancellationToken ct)
    {
        if (fact.Operation is null || !AdvisorNativeHistory.Reference(fact.Intent, ChildAdmissionIntent.Schema))
            throw Invalid("The feedback contribution lacks its original admitted intent.");
        var intent = await AdvisorContent.ReadAsync<ChildAdmissionIntent>(content, fact.TenantId, fact.Intent, ct)
            .ConfigureAwait(false);
        if (intent.Header is null || !AdvisorNativeHistory.Reference(intent.CanonicalAuthoredContent,
                AdvisorContent.FeedbackAuthoredSchema))
            throw Invalid("The admitted feedback lacks its complete canonical authored content.");
        var authored = await AdvisorContent.ReadAsync<RecordAdvisorFeedback>(content, fact.TenantId,
            intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
        var parent = new AdvisorHandoff(fact.TenantId, fact.ProgramId, fact.HandoffId).Stream;
        var target = new AdvisorFeedbackThread(fact.TenantId, fact.ProgramId, fact.HandoffId, fact.FeedbackId).Stream;
        var header = intent.Header;
        var source = authored.SelectedSource ?? new AdvisorRecordVersion(authored.RecordId, authored.RecordVersionId);
        if (authored.TenantId == Uuid.Empty || authored.ProgramId == Uuid.Empty || authored.HandoffId == Uuid.Empty ||
            authored.FeedbackId == Uuid.Empty || authored.RecordId == Uuid.Empty || !source.HasValidIdentity ||
            authored.SelectedSource is { IsSupported: false } || !AdvisorHandoffRules.FeedbackKinds.Contains(authored.Kind) ||
            intent.ChildKind != AdmittedChildKind.AdvisorFeedback || intent.ChildId != fact.FeedbackId ||
            intent.ParentId != fact.HandoffId || intent.ParentGeneration == 0 ||
            authored.TenantId != fact.TenantId || authored.ProgramId != fact.ProgramId ||
            authored.HandoffId != fact.HandoffId || authored.FeedbackId != fact.FeedbackId ||
            authored.RecordId != fact.RecordId || authored.RecordVersionId != fact.RecordVersionId ||
            authored.Kind != fact.Kind || authored.SelectedSource != fact.SelectedSource ||
            string.IsNullOrWhiteSpace(authored.Body) || authored.Body.Trim() != authored.Body ||
            authored.ExpectedHandoffRevision < 0 || header.OriginalExpectedRevision != (ulong)authored.ExpectedHandoffRevision ||
            intent.ParentAdmissionRevision != (ulong)authored.ExpectedHandoffRevision + 1 ||
            header.RootOperationId != fact.FeedbackId || header.FirstOwner != parent ||
            header.AffectedOwner != parent || header.ExpectedRevisionOwner != parent ||
            header.OriginalActor != fact.Author || header.AcceptedAt != fact.CreatedAt ||
            header.Episode != fact.Operation.Episode || header.AuthoredIntentSha256 != fact.Operation.AuthoredIntentSha256 ||
            AuthoredIntentDigest.Compute(authored) != header.AuthoredIntentSha256 ||
            fact.Operation.LogicalId == Uuid.Empty || fact.Operation.CommandKind != nameof(MaterializeAdvisorFeedback) ||
            fact.Operation.Episode != AdvisorContent.InitialEpisode || fact.Operation.AcceptedAt == default ||
            fact.Operation.CausationEventId is null || fact.Operation.CausationEventId == Uuid.Empty ||
            !AdvisorHandoffRules.ValidActor(fact.Author) ||
            fact.Operation.Owner != target || fact.Operation.AffectedOwner != target ||
            fact.Operation.ExpectedRevisionOwner != target || fact.Operation.FirstOwner != header.FirstOwner ||
            fact.Operation.RootOperationId != header.RootOperationId || fact.Operation.Actor != header.OriginalActor ||
            fact.Operation.OriginalExpectedRevision != header.OriginalExpectedRevision)
            throw Invalid("The original feedback contribution differs from its exact accepted native intent and lineage.");
        return new AdvisorFeedbackRecordedContent(fact, authored.Body);
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
