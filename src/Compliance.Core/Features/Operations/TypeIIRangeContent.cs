using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads and writes the immutable content referenced by Type II range facts; the realm is the tenant.</summary>
static class TypeIIRangeContent
{
    /// <summary>Where a caller reads the status of an accepted operation.</summary>
    public const string StatusUrlTemplate = "/api/v1/platform/operations/{operation_id}";

    public static Task<TypeIIRangeIntent> ReadIntentAsync(IImmutableContentStore content, string realm,
        ImmutableContentRef reference, CancellationToken ct) =>
        content.ReadJsonAsync<TypeIIRangeIntent>(realm, reference, ct);

    /// <summary>Writes the Unit result content a terminal fact references as its operation result.</summary>
    public static async Task<ImmutableContentRef> UnitResultAsync(IImmutableContentStore content, string realm,
        Uuid rootOperationId, CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(rootOperationId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return await content.WriteJsonAsync(realm, Uuid.CreateVersion5(rootOperationId, "mutation-result-content.v1"),
            "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)),
            ct).ConfigureAwait(false);
    }

    /// <summary>The frozen 202 first response of a root admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content,
        string realm, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var acknowledgment = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            "operation-accepted-response.v1", new OperationAcceptedResponse(operationId,
                StatusUrlTemplate.Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal),
                acceptedAt), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
            "operation-accepted-response.v1", new StandardResponseBody(acknowledgment));
    }

    /// <summary>The 202 acknowledgment of an accepted or replayed admission, read from the frozen first response.</summary>
    public static async Task<Result<OperationAcceptedResponse>> AcknowledgmentAsync(IImmutableContentStore content,
        string realm, Result<AcceptedAppendResult> outcome, CancellationToken ct)
    {
        if (!outcome.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(outcome.Error!);
        var descriptor = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<OperationAcceptedResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("A Type II range acknowledgment is standard content.");
        return Result<OperationAcceptedResponse>.Success(await content
            .ReadJsonAsync<OperationAcceptedResponse>(realm, body.Content, ct).ConfigureAwait(false));
    }
}
