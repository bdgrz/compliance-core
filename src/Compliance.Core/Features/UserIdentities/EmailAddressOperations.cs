using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>What the native admissions of the email address commands share: the actor, the provenance and the frozen Unit response.</summary>
static class EmailAddressOperations
{
    /// <summary>The realm every email address lives in.</summary>
    public const string Realm = "bdgrz";

    /// <summary>The original actor of an admission: the system process or the authenticated user.</summary>
    public static ActorReference? ActorOf(ClaimsPrincipal principal)
    {
        if (RequestActor.IsSystem(principal))
            return ActorReference.ForSystemProcess("system", "system");
        return UserIdentityClaims.TryGetBdgrzSubject(principal, out var userId)
            ? new ActorReference("user", userId.ToString(), UserIdentityClaims.BdgrzDisplay(principal, userId))
            : null;
    }

    /// <summary>The provenance of a root admission on <paramref name="owner" />; a retry's binding is derived from it.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, ActorReference actor,
        EventStreamAddress owner, long expectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(logicalId, logicalId, null, commandKind, actor, owner, logicalId.ToString(),
            expectedRevision >= 0 ? (ulong)expectedRevision : null, authoredIntentSha256, acceptedAt, owner, owner);

    /// <summary>Freezes the Unit response of an admission as verified immutable content.</summary>
    public static async Task<FirstResponseDescriptor> UnitResponseAsync(IImmutableContentStore content,
        CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), UnitResult.Schema, new UnitResult(), ct)
            .ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(unit));
    }

    /// <summary>The closed outcome of a root admission as a handler result.</summary>
    public static Result Of(Result<AcceptedAppendResult> accepted) => !accepted.IsSuccess
        ? Result.Failure(accepted.Error!)
        : accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
}
