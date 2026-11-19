using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

/// <summary>Reconstructs one target-scoped receipt from exact qualified source facts.</summary>
public sealed class SourceCriteriaEditionValueOperationReader(ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaPublicationAuthority? publications = null,
    ICriteriaFreshEntryHead? freshHead = null, ICriteriaParticipantCausalOrder? causalOrder = null)
    : ICriteriaEditionValueOperationReader
{
    /// <inheritdoc />
    public async ValueTask<Result<CriteriaOperationReceipt>> ReadAsync(Uuid editionId, string targetKind, string? field,
        string? category, string? code, Uuid operationId, CancellationToken ct = default)
    {
        if (editionId == Uuid.Empty || operationId == Uuid.Empty ||
            !CriteriaEditionValueRules.Target(targetKind, field, category, code))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Validation,
                "The exact edition, target and operation are required."));
        if (coverage is null || publications is null || freshHead is null || causalOrder is null)
            return Pending(editionId, operationId);
        try
        {
            var target = new CriteriaEditionValue(editionId, targetKind, field, category, code);
            CriteriaOrderedSourceEvent? proposalRecord = null, outcomeRecord = null;
            ulong next = 0;
            await foreach (var item in source.ReadAsync(target.Stream, target.Id, ct).ConfigureAwait(false))
            {
                if (!Exact(item, target.Stream, target.Id) || item.ResourceOffset != next++)
                    return Pending(editionId, operationId);
                if (item.Event is CriteriaEditionValueProposed proposed && proposed.Intent.OperationId == operationId)
                {
                    if (proposalRecord is not null)
                        return Pending(editionId, operationId);
                    proposalRecord = item;
                }
                var outcomeOperation = item.Event switch
                {
                    CriteriaEditionValueAccepted accepted => accepted.OperationId,
                    CriteriaEditionValueRefused refused => refused.OperationId,
                    _ => Uuid.Empty,
                };
                if (outcomeOperation == operationId)
                {
                    if (outcomeRecord is not null)
                        return Pending(editionId, operationId);
                    outcomeRecord = item;
                }
            }
            if (proposalRecord?.Event is not CriteriaEditionValueProposed proposal || outcomeRecord is null ||
                !CriteriaEditionValueRules.Shape(proposal.Intent) || proposal.Intent.EditionId != editionId ||
                proposal.Intent.TargetKind != targetKind || proposal.Intent.Field != field || proposal.Intent.Category != category ||
                proposal.Intent.Code != code || proposal.Intent.OperationId != operationId ||
                !CriteriaEditionValueRules.Admission(proposal.Intent, proposal.ExecutionGeneration, proposal.Actor, proposal.Admission) ||
                proposal.OperationClaimEventId == Uuid.Empty || proposalRecord.ResourceOffset >= outcomeRecord.ResourceOffset ||
                proposalRecord.AreaOffset >= outcomeRecord.AreaOffset)
                return Pending(editionId, operationId);
            if (!await coverage.CoversAsync(target.Stream, next, outcomeRecord.AreaOffset, ct).ConfigureAwait(false))
                return Pending(editionId, operationId);

            var claimOwner = new CriteriaEditionValueOperation(editionId, targetKind, field, category, code, operationId);
            CriteriaOrderedSourceEvent? claimRecord = null;
            ulong claimNext = 0;
            await foreach (var item in source.ReadAsync(claimOwner.Stream, claimOwner.Id, ct).ConfigureAwait(false))
            {
                if (!Exact(item, claimOwner.Stream, claimOwner.Id) || item.ResourceOffset != claimNext++ || claimRecord is not null)
                    return Pending(editionId, operationId);
                claimRecord = item;
            }
            if (claimRecord?.Event is not CriteriaEditionValueOperationClaimed claim || claimRecord.ResourceOffset != 0 ||
                claim.Metadata.EventId != proposal.OperationClaimEventId || claim.Intent != proposal.Intent ||
                claim.ExecutionGeneration != proposal.ExecutionGeneration || claim.Actor != proposal.Actor ||
                claim.Admission is null || proposal.Admission is null || claim.Admission.UserId != proposal.Admission.UserId ||
                !CriteriaEditionValueRules.Admission(claim.Intent, claim.ExecutionGeneration, claim.Actor, claim.Admission) ||
                claimRecord.AreaOffset >= proposalRecord.AreaOffset ||
                !await coverage.CoversAsync(claimOwner.Stream, claimNext, proposalRecord.AreaOffset, ct).ConfigureAwait(false))
                return Pending(editionId, operationId);
            if (!await new CriteriaValueReceiptDraftVerifier(source).VerifyAsync(proposal, claimRecord.AreaOffset, ct)
                    .ConfigureAwait(false))
                return Pending(editionId, operationId);

            var verified = await new CriteriaEditionValueDecisionEvidenceVerifier(source, coverage, publications, freshHead, causalOrder)
                .VerifyAsync(proposalRecord, outcomeRecord, ct).ConfigureAwait(false);
            return outcomeRecord.Event switch
            {
                CriteriaEditionValueAccepted when verified == CriteriaEntryDecisionVerification.Verified =>
                    Result<CriteriaOperationReceipt>.Success(new CriteriaOperationReceipt(editionId, operationId, "accepted")),
                CriteriaEditionValueRefused refused when verified == CriteriaEntryDecisionVerification.Verified ||
                    verified == CriteriaEntryDecisionVerification.Abandoned && refused.Reason == "edition_abandoned" =>
                    Result<CriteriaOperationReceipt>.Success(new CriteriaOperationReceipt(editionId, operationId, "refused", refused.Reason)),
                _ => Pending(editionId, operationId),
            };
        }
        catch (CriteriaSourceUnavailableException) { return Pending(editionId, operationId); }
        catch (FitzException) { return Pending(editionId, operationId); }
        catch (IOException) { return Pending(editionId, operationId); }
        catch (TimeoutException) { return Pending(editionId, operationId); }
    }

    static bool Exact(CriteriaOrderedSourceEvent item, EventStreamAddress stream, Uuid id) => item.Stream == stream &&
        item.Event.Metadata.AggregateId == id && item.Event.Metadata.EventId != Uuid.Empty && !item.Event.Metadata.IsAudit &&
        item.Event.Metadata.AggregateVersion == checked(item.ResourceOffset + 1);
    static Result<CriteriaOperationReceipt> Pending(Uuid editionId, Uuid operationId) =>
        Result<CriteriaOperationReceipt>.Success(new CriteriaOperationReceipt(editionId, operationId, "pending"));
}
