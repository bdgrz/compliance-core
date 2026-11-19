using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Qualifies current operator authority for one exact, domain-separated criteria read.</summary>
public static class CriteriaOperatorReadAdmission
{
    /// <summary>
    /// Obtains server-produced admission for the requested source and read purpose. The caller
    /// supplies a stable nonempty operation identity; reads without a user operation may derive
    /// their own stable scoped identity. No caller-provided evidence is accepted.
    /// </summary>
    public static async ValueTask<Result> AuthorizeAsync(IAccessAdmissionAuthority? authority, IRequestContext context,
        Uuid editionId, Uuid operationId, EventStreamAddress target, string purpose, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var actor = context.Actor;
        if (editionId == Uuid.Empty || operationId == Uuid.Empty || target is null ||
            target.Realm != "bdgrz" || target.Area != $"criteria-{editionId}" ||
            purpose is not ("edition-draft" or "operation" or "edition-value-operation"))
            return Failure(RequestErrorKind.Validation, "The exact criteria read scope is required.", false);
        if (RequestActor.IsSystem(actor) || !UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId))
            return Failure(RequestErrorKind.Forbidden, "A currently authorized human platform operator is required.", false);
        var identity = actor.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated &&
            candidate.FindFirst("iss")?.Value == "bdgrz");
        var subject = identity?.FindFirst("sub")?.Value;
        var issuer = identity?.FindFirst("iss")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer))
            return Failure(RequestErrorKind.Forbidden, "A currently authorized human platform operator is required.", false);
        if (authority is null)
            return Unavailable();
        var digest = ContentDigest.Sha256(Encoding.UTF8.GetBytes(
            $"bdgrz.criteria.operator-read.v1\n{purpose}\n{editionId}\n{target}\n{operationId}"));
        try
        {
            var evidence = await authority.AdmitCriteriaOperatorAsync(context, operationId, target.ToString(), digest, ct)
                .ConfigureAwait(false);
            if (evidence is not null && !AccessAdmissionEvidenceBinding.IsCurrentFormat(evidence))
                return Unavailable();
            return evidence is not null && AccessAdmissionEvidenceBinding.BindsCriteriaOperator(evidence, userId,
                new ActorAttribution(subject, issuer), operationId, target.ToString(), digest)
                ? Result.Success : Failure(RequestErrorKind.Forbidden, "Current platform operator authority is required.", false);
        }
        catch (CriteriaSourceUnavailableException) { return Unavailable(); }
        catch (FitzException) { return Unavailable(); }
        catch (IOException) { return Unavailable(); }
        catch (TimeoutException) { return Unavailable(); }
    }

    static Result Unavailable() => Failure(RequestErrorKind.Conflict,
        "Current source-backed operator authority is unavailable.", true);
    static Result Failure(RequestErrorKind kind, string message, bool transient) =>
        Result.Failure(new RequestError(kind, message, transient));
}
