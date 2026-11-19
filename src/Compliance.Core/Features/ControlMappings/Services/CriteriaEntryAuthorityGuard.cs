using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Services;

/// <summary>Requires exact source-backed published membership before proposing a criterion decision.</summary>
static class CriteriaEntryAuthorityGuard
{
    public static async ValueTask<Result<Criterion>> RequireAsync(ICriteriaEntryAuthority authority, Uuid editionId,
        string identifier, CancellationToken ct = default)
    {
        var result = await RequireProofAsync(authority, editionId, identifier, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<Criterion>.Success(result.Value.Entry)
            : Result<Criterion>.Failure(result.Error!);
    }

    public static async ValueTask<Result<CriteriaEntryProof>> RequireProofAsync(ICriteriaEntryAuthority authority,
        Uuid editionId, string identifier, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        var resolved = await authority.ReadAsync(editionId, identifier, ct).ConfigureAwait(false);
        if (!resolved.IsSuccess)
            return Result<CriteriaEntryProof>.Failure(resolved.Error!);

        var value = resolved.Value;
        if (value.Status == CriteriaEntryAuthorityStatus.Pending && value.Proof is null)
            return Failure(RequestErrorKind.Conflict, "The criterion's published source outcome is still pending.", true);
        if (value.Status == CriteriaEntryAuthorityStatus.NotMember && value.Proof is null)
            return Failure(RequestErrorKind.Validation, "The criterion is not a member of the published edition.");
        if (value.Status != CriteriaEntryAuthorityStatus.Accepted || value.Proof is not { } proof ||
            proof.Entry is not { } entry || entry.EditionId != editionId ||
            !string.Equals(entry.Identifier, identifier, StringComparison.Ordinal) ||
            proof.IncarnationId == Uuid.Empty || proof.PublicationEventId == Uuid.Empty ||
            proof.ProposalEventId == Uuid.Empty || proof.DecisionEventId == Uuid.Empty)
            return Failure(RequestErrorKind.Conflict, "The criterion's published source evidence is invalid.");

        if (entry.Kind == "criterion")
        {
            if (entry.ParentIdentifier is not null || proof.ParentIncarnationId is not null ||
                proof.ParentProposalEventId is not null || proof.ParentDecisionEventId is not null)
                return Failure(RequestErrorKind.Conflict, "A criterion has invalid parent source evidence.");
        }
        else if (entry.Kind == "point_of_focus")
        {
            if (string.IsNullOrWhiteSpace(entry.ParentIdentifier) ||
                proof.ParentIncarnationId is not { } parent || parent == Uuid.Empty ||
                proof.ParentProposalEventId is not { } proposal || proposal == Uuid.Empty ||
                proof.ParentDecisionEventId is not { } decision || decision == Uuid.Empty)
                return Failure(RequestErrorKind.Conflict, "The point of focus has no exact effective-parent evidence.");
        }
        else
            return Failure(RequestErrorKind.Conflict, "The criterion has an invalid kind.");

        return Result<CriteriaEntryProof>.Success(proof);
    }

    static Result<CriteriaEntryProof> Failure(RequestErrorKind kind, string message, bool transient = false) =>
        Result<CriteriaEntryProof>.Failure(new RequestError(kind, message, isTransient: transient));
}
