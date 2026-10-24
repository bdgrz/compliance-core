using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Guards and result shaping shared by the finding and corrective action command handlers.</summary>
static class FindingCommands
{
    public static async ValueTask<bool> IsActiveMemberAsync(IAggregateReader reader, Uuid tenantId, Uuid memberId,
        CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct).ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && member.Affiliation != "firm_staff";
    }

    public static RequestError InactiveOwner() => new(RequestErrorKind.Validation,
        "The owner must be an active client member.");

    /// <summary>The first response of a prepared command, or a retryable conflict when the owner changed while it was prepared.</summary>
    public static Result<FirstResponseDescriptor> Prepared(Result guard, FirstResponseDescriptor? response) =>
        !guard.IsSuccess
            ? Result<FirstResponseDescriptor>.Failure(guard.Error!)
            : response is null
                ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The record changed while the command was prepared; reload and retry.", true))
                : Result<FirstResponseDescriptor>.Success(response);

    public static string Digest<T>(T request) where T : notnull => AuthoredIntentDigest.Compute(request);
}
