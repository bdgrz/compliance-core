using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Freezes and maps the first response of an operation whose declared result is Unit.</summary>
public static class UnitResponses
{
    /// <summary>Writes the empty result content and describes it as the HTTP 200 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeAsync(IImmutableContentStore content, string realm,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), UnitResult.Schema, new UnitResult(), ct)
            .ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(body));
    }

    /// <summary>
    ///     Maps a closed acceptance to a Unit result. A commit or exact replay succeeds, a changed binding conflicts,
    ///     and any error listed in <paramref name="terminalNoOps" /> is a named terminal outcome that needs no append,
    ///     such as a receiver that arrives after the owner moved past what it answers.
    /// </summary>
    public static Result ToResult(Result<AcceptedAppendResult> accepted, params RequestError[] terminalNoOps)
    {
        if (!accepted.IsSuccess)
            return Array.IndexOf(terminalNoOps, accepted.Error) >= 0 ? Result.Success : Result.Failure(accepted.Error!);
        return accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
    }
}
