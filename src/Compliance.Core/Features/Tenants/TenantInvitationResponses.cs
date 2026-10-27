using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Freezes the immediate Unit response of the invitation commands.</summary>
static class TenantInvitationResponses
{
    /// <summary>Writes the Unit result content and describes it as the HTTP 200 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeUnitAsync(IImmutableContentStore content, string realm,
        Uuid logicalId, CancellationToken ct)
    {
        var result = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(logicalId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(result));
    }

    /// <summary>The result of a closed acceptance outcome: committed and replayed succeed, a changed intent conflicts.</summary>
    public static Result Of(Result<AcceptedAppendResult> accepted) =>
        !accepted.IsSuccess
            ? Result.Failure(accepted.Error!)
            : accepted.Value is AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed
                ? Result.Success
                : Result.Failure(AcceptedAppendResult.ConflictError);
}
