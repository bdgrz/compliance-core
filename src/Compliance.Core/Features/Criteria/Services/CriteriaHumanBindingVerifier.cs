using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Verifies the independently committed operation claim for one human entry proposal.</summary>
sealed class CriteriaHumanBindingVerifier(ICriteriaExactSourceReader source)
{
    internal async ValueTask<bool> VerifyProposalAsync(CriterionChangeProposed proposal, ulong proposalAreaOffset,
        CancellationToken ct)
    {
        var intent = proposal.Intent;
        if (!CriteriaContentBounds.StructuralShape(intent) || proposal.OperationClaimEventId == Uuid.Empty ||
            proposal.Actor is null || proposal.Admission is null || proposal.ExecutionGeneration <= 0 ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(proposal.Admission, proposal.Admission.UserId,
                proposal.Actor, intent.OperationId, CriteriaEntry.StreamFor(intent.EditionId, intent.Identifier).ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), proposal.ExecutionGeneration)))
            throw new InvalidOperationException("A human proposal requires its exact operation claim.");
        var operation = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var record = await source.FindAsync(operation.Stream, operation.Id, proposal.OperationClaimEventId, ct)
            .ConfigureAwait(false);
        if (record is null)
            return false;
        if (record.Stream != operation.Stream || record.Event is not CriterionEntryOperationClaimed claim ||
            claim.Metadata.EventId != proposal.OperationClaimEventId || claim.Metadata.AggregateId != operation.Id ||
            claim.Metadata.IsAudit || claim.Structural != intent || claim.Summary is not null ||
            claim.Actor != proposal.Actor || claim.Admission is null || claim.ExecutionGeneration <= 0 ||
            claim.Admission.UserId != proposal.Admission.UserId ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(claim.Admission, claim.Admission.UserId,
                claim.Actor, intent.OperationId, CriteriaEntry.StreamFor(intent.EditionId, intent.Identifier).ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), claim.ExecutionGeneration)) ||
            record.AreaOffset >= proposalAreaOffset)
            throw new InvalidOperationException("The human proposal does not match its preceding operation claim.");

        // Later generations need an independently qualified target-fence proof.
        return claim.ExecutionGeneration == proposal.ExecutionGeneration;
    }

    internal async ValueTask<bool> VerifySummaryAsync(CriterionSummaryCorrected correction, ulong correctionAreaOffset,
        CancellationToken ct) => await VerifySummaryAsync(correction.Intent, correction.ExecutionGeneration,
            correction.Actor, correction.Admission, correction.OperationClaimEventId, correctionAreaOffset, ct).ConfigureAwait(false);

    internal async ValueTask<bool> VerifySummaryAsync(CriterionSummaryUnchanged unchanged, ulong correctionAreaOffset,
        CancellationToken ct) => await VerifySummaryAsync(unchanged.Intent, unchanged.ExecutionGeneration,
            unchanged.Actor, unchanged.Admission, unchanged.OperationClaimEventId, correctionAreaOffset, ct).ConfigureAwait(false);

    internal async ValueTask<bool> VerifySummaryAsync(CriterionSummaryRefused refused, ulong correctionAreaOffset,
        CancellationToken ct) => await VerifySummaryAsync(refused.Intent, refused.ExecutionGeneration,
            refused.Actor, refused.Admission, refused.OperationClaimEventId, correctionAreaOffset, ct).ConfigureAwait(false);

    async ValueTask<bool> VerifySummaryAsync(CriterionSummaryIntent intent, long executionGeneration,
        ActorAttribution actor, AccessAdmissionEvidence admission, Uuid claimEventId, ulong correctionAreaOffset,
        CancellationToken ct)
    {
        if (!CriteriaContentBounds.SummaryIntent(intent) || claimEventId == Uuid.Empty ||
            actor is null || admission is null || executionGeneration <= 0 ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId,
                actor, intent.OperationId, CriteriaEntry.StreamFor(intent.EditionId, intent.Identifier).ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), executionGeneration)))
            throw new InvalidOperationException("A summary correction requires its exact admitted operation claim.");
        var operation = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var record = await source.FindAsync(operation.Stream, operation.Id, claimEventId, ct)
            .ConfigureAwait(false);
        if (record is null)
            return false;
        if (record.Stream != operation.Stream || record.Event is not CriterionEntryOperationClaimed claim ||
            claim.Metadata.EventId != claimEventId || claim.Metadata.AggregateId != operation.Id ||
            claim.Metadata.IsAudit || claim.Structural is not null || claim.Summary != intent ||
            claim.Actor != actor || claim.Admission is null || claim.ExecutionGeneration <= 0 ||
            claim.Admission.UserId != admission.UserId ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(claim.Admission, claim.Admission.UserId,
                claim.Actor, intent.OperationId, CriteriaEntry.StreamFor(intent.EditionId, intent.Identifier).ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), claim.ExecutionGeneration)) ||
            record.AreaOffset >= correctionAreaOffset)
            throw new InvalidOperationException("The summary correction does not match its preceding operation claim.");
        return claim.ExecutionGeneration == executionGeneration;
    }
}
