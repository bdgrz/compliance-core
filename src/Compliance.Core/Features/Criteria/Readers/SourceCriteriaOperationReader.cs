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

/// <summary>Reads criteria operation states from the owners' committed histories.</summary>
/// <remarks>
/// An operation not observed in source is pending rather than refused: a delayed or
/// ambiguous commit may still arrive. The caller must independently authorize access.
/// </remarks>
public sealed class SourceCriteriaOperationReader(IAggregateReader reader,
    ICriteriaExactSourceReader? source = null, ICriteriaSourceCutCoverage? coverage = null,
    ICriteriaPublicationAuthority? publications = null, ICriteriaFreshEntryHead? entryHead = null,
    ICriteriaFreshEditionHead? editionHead = null, ICriteriaParticipantCausalOrder? causal = null) : ICriteriaOperationReader
{
    /// <inheritdoc />
    public async ValueTask<Result<CriteriaEditionDraftView>> GetDraftAsync(Uuid editionId,
        CancellationToken ct = default)
    {
        if (editionId == Uuid.Empty)
            return Result<CriteriaEditionDraftView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A criteria edition identity is required."));

        var edition = await reader.HydrateAsync(new CriteriaEdition(editionId), ct).ConfigureAwait(false);
        if (editionHead is null || !await editionHead.IsCurrentAsync(edition.Stream, edition.Id,
                edition.CommittedStreamPosition, ct).ConfigureAwait(false))
            return Result<CriteriaEditionDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The edition source is not currently qualified.", true));
        var status = edition.Status == "empty" ? "pending" : edition.Status;
        var pending = edition.PendingOperationId == Uuid.Empty ? (Uuid?)null : edition.PendingOperationId;
        return Result<CriteriaEditionDraftView>.Success(new CriteriaEditionDraftView(editionId, status,
            edition.Label ?? edition.RequestedLabel, pending, Framework: edition.Framework));
    }

    /// <inheritdoc />
    public async ValueTask<Result<CriteriaOperationReceipt>> GetOperationAsync(Uuid editionId, Uuid operationId,
        string? identifier, CancellationToken ct = default)
    {
        if (editionId == Uuid.Empty || operationId == Uuid.Empty ||
            identifier is not null && !CriteriaContentBounds.Identifier(identifier))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Validation,
                "A criteria edition, operation and valid entry identity are required."));

        CriteriaOperationReceipt? receipt;
        if (identifier is null)
        {
            var edition = await reader.HydrateAsync(new CriteriaEdition(editionId, operationId), ct).ConfigureAwait(false);
            receipt = edition.Receipt(operationId);
            if (receipt is not null && receipt.Status != "pending")
            {
                if (!await VerifyEditionReceiptAsync(edition, operationId, receipt.Status, ct).ConfigureAwait(false))
                    receipt = null;
            }
        }
        else
        {
            var entry = await reader.HydrateAsync(new CriteriaEntry(editionId, identifier,
                selectedOperationId: operationId), ct).ConfigureAwait(false);
            receipt = entry.Receipt(operationId);
            if (receipt is not null && receipt.Status != "pending" &&
                !await VerifyEntryReceiptAsync(entry, operationId, receipt.Status, ct).ConfigureAwait(false))
                receipt = null;
        }

        return Result<CriteriaOperationReceipt>.Success(receipt ??
            new CriteriaOperationReceipt(editionId, operationId, "pending"));
    }

    async ValueTask<bool> VerifyEditionReceiptAsync(CriteriaEdition edition, Uuid operationId, string status,
        CancellationToken ct)
    {
        // The selected edition owner records the terminal participant decision;
        // qualify the replayed head before reporting that exact operation.
        if (editionHead is null)
            return false;
        try
        {
            if (!await editionHead.IsCurrentAsync(edition.Stream, edition.Id, edition.CommittedStreamPosition, ct)
                    .ConfigureAwait(false))
                return false;
            if (status != "accepted" || edition.Publication is not { } published ||
                published.PublicationAttemptId != operationId)
                return true;
            if (publications is null)
                return false;
            var proof = await publications.ReadPublishedAsync(edition.Id, ct).ConfigureAwait(false);
            return proof.IsSuccess && proof.Value is not null && proof.Value.EventId == published.Metadata.EventId;
        }
        catch (CriteriaSourceUnavailableException) { return false; }
        catch (FitzException) { return false; }
        catch (IOException) { return false; }
        catch (TimeoutException) { return false; }
    }

    async ValueTask<bool> VerifyEntryReceiptAsync(CriteriaEntry entry, Uuid operationId, string status,
        CancellationToken ct)
    {
        if (source is null || coverage is null || publications is null || entryHead is null)
            return false;
        try
        {
            CriteriaOrderedSourceEvent? proposal = null, decision = null, summary = null;
            ulong next = 0;
            await foreach (var record in source.ReadAsync(entry.Stream, entry.Id, ct).ConfigureAwait(false))
            {
                if (record.Stream != entry.Stream || record.ResourceOffset != next++ ||
                    record.Event.Metadata.AggregateId != entry.Id || record.Event.Metadata.IsAudit)
                    throw new InvalidOperationException("The entry operation source is not contiguous.");
                if (record.Event is CriterionChangeProposed human && human.Intent.OperationId == operationId ||
                    record.Event is CriterionSeedProposed seed && seed.Intent.OperationId == operationId)
                {
                    if (proposal is not null)
                        throw new InvalidOperationException("An operation has multiple proposals.");
                    proposal = record;
                }
                else if (record.Event is CriterionChanged changed && changed.OperationId == operationId ||
                         record.Event is CriterionChangeRefused refused && refused.OperationId == operationId)
                {
                    if (decision is not null)
                        throw new InvalidOperationException("An operation has multiple decisions.");
                    decision = record;
                }
                else if (record.Event is CriterionSummaryCorrected correction && correction.Intent.OperationId == operationId)
                {
                    if (summary is not null)
                        throw new InvalidOperationException("An operation has multiple summary corrections.");
                    summary = record;
                }
                else if (record.Event is CriterionSummaryUnchanged noChange && noChange.Intent.OperationId == operationId)
                {
                    if (summary is not null)
                        throw new InvalidOperationException("An operation has multiple summary outcomes.");
                    summary = record;
                }
                else if (record.Event is CriterionSummaryRefused refusal && refusal.Intent.OperationId == operationId)
                {
                    if (summary is not null)
                        throw new InvalidOperationException("An operation has multiple summary outcomes.");
                    summary = record;
                }
            }
            if (next != entry.CommittedStreamPosition ||
                !await entryHead.IsCurrentAsync(entry.Stream, entry.Id, next, ct).ConfigureAwait(false))
                return false;
            if (summary?.Event is CriterionSummaryCorrected corrected)
                return status == "accepted" && proposal is null && decision is null &&
                    await new CriteriaHumanBindingVerifier(source).VerifySummaryAsync(corrected, summary.AreaOffset, ct)
                        .ConfigureAwait(false);
            if (summary?.Event is CriterionSummaryUnchanged unchanged)
                return status == "accepted" && proposal is null && decision is null &&
                    await new CriteriaHumanBindingVerifier(source).VerifySummaryAsync(unchanged, summary.AreaOffset, ct)
                        .ConfigureAwait(false);
            if (summary?.Event is CriterionSummaryRefused summaryRefused)
                return status == "refused" && proposal is null && decision is null &&
                    await new CriteriaHumanBindingVerifier(source).VerifySummaryAsync(summaryRefused, summary.AreaOffset, ct)
                        .ConfigureAwait(false);
            if (proposal is null || decision is null || summary is not null ||
                (status == "accepted") != (decision.Event is CriterionChanged))
                return false;
            var claimVerified = proposal.Event switch
            {
                CriterionChangeProposed human => await new CriteriaHumanBindingVerifier(source)
                    .VerifyProposalAsync(human, proposal.AreaOffset, ct).ConfigureAwait(false),
                CriterionSeedProposed seed => await new CriteriaSeedBindingVerifier(source)
                    .VerifyProposalAsync(seed, proposal.AreaOffset, ct).ConfigureAwait(false),
                _ => false,
            };
            if (!claimVerified)
                return false;
            var (intent, parentAccepted) = proposal.Event switch
            {
                CriterionChangeProposed human => (human.Intent, human.ParentAcceptedEventId),
                CriterionSeedProposed seed => (seed.Intent, seed.ParentAcceptedEventId),
                _ => throw new InvalidOperationException("The entry proposal has an unknown type."),
            };
            if (status == "accepted" && intent.Kind == "point_of_focus" && intent.Action != "remove" &&
                !await CriteriaEntryDecisionAuthority.ParentActiveAsync(source, coverage, publications, intent,
                    parentAccepted, proposal.AreaOffset, ct, causal).ConfigureAwait(false))
                return false;
            var verified = await new CriteriaEntryDecisionEvidenceVerifier(source, coverage, publications, causal)
                .VerifyAsync(proposal, decision, ct).ConfigureAwait(false);
            return verified == CriteriaEntryDecisionVerification.Verified ||
                status == "refused" && verified == CriteriaEntryDecisionVerification.Abandoned;
        }
        catch (CriteriaSourceUnavailableException) { return false; }
        catch (FitzException) { return false; }
        catch (IOException) { return false; }
        catch (TimeoutException) { return false; }
    }
}
