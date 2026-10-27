using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     What the immediate-answer inventory operations share: the provenance an operation freezes, the original 200
///     answer frozen before the append, and reading that answer back for the first call and every exact retry.
/// </summary>
static class InventoryOperations
{
    /// <summary>The provenance of one inventory operation on its own record owner.</summary>
    public static OperationProvenance Provenance(EventStreamAddress owner, Uuid logicalId, string commandKind,
        ActorReference actor, long? expectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(logicalId, logicalId, null, commandKind, actor, owner, expectedRevision is null ? "initial" : "current",
            expectedRevision is >= 0 ? (ulong)expectedRevision : null, authoredIntentSha256, acceptedAt, owner, owner);

    /// <summary>Writes the original answer and describes it as the HTTP 200 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeAsync<T>(IImmutableContentStore content, string realm,
        string schema, T answer, CancellationToken ct) where T : notnull
    {
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), schema, answer, ct)
            .ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, schema,
            new StandardResponseBody(body));
    }

    /// <summary>The answer a committed or replayed acceptance froze, read back as verified content.</summary>
    public static async Task<Result<T>> ReadAsync<T>(IImmutableContentStore content, string realm, string schema,
        Result<AcceptedAppendResult> accepted, CancellationToken ct) where T : notnull
    {
        if (!accepted.IsSuccess)
            return Result<T>.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body || descriptor.ResultSchema != schema)
            throw new ImmutableContentIntegrityException("The accepted inventory operation froze an unexpected answer.");
        return Result<T>.Success(await content.ReadJsonAsync<T>(realm, body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The outcome of an operation whose declared result is Unit.</summary>
    public static async Task<Result> ReadUnitAsync(IImmutableContentStore content, string realm,
        Result<AcceptedAppendResult> accepted, CancellationToken ct)
    {
        var answer = await ReadAsync<UnitResult>(content, realm, UnitResult.Schema, accepted, ct)
            .ConfigureAwait(false);
        return answer.IsSuccess ? Result.Success : Result.Failure(answer.Error!);
    }
}
