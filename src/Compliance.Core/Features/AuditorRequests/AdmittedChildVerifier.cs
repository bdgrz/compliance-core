using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Finds the admission a materializing receiver was built from on the authoritative request. The source reference
///     only names the fact; nothing a command states is trusted until the request confirms it. An admission that is not
///     visible yet is transient and a differing one is a permanent conflict.
/// </summary>
static class AdmittedChildVerifier
{
    public static async ValueTask<Result<TFact>> VerifyAsync<TFact>(IAggregateReader reader, Uuid tenantId, Uuid programId,
        Uuid requestId, NativeFactReference source, ImmutableContentRef intent, Uuid operationId, Uuid logicalId,
        Func<TFact, ImmutableContentRef> intentOf, CancellationToken ct) where TFact : DomainEvent, INativeOperationFact
    {
        var parent = await reader.HydrateAsync(new AuditorRequest(tenantId, programId, requestId, logicalId), ct)
            .ConfigureAwait(false);
        if (parent.Facts.FirstOrDefault(fact => fact.Metadata?.EventId == source.EventId) is not TFact admitted)
            return Fail<TFact>("The admission is not visible on its request yet.", true);
        if (admitted.Metadata.AggregateVersion != source.AggregateVersion || intentOf(admitted) != intent ||
            admitted.Operation.LogicalId != logicalId || admitted.Operation.RootOperationId != operationId)
            return Fail<TFact>("The source differs from the admission of this child.", false);
        return Result<TFact>.Success(admitted);
    }

    static Result<TFact> Fail<TFact>(string message, bool transient) where TFact : DomainEvent, INativeOperationFact =>
        Result<TFact>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
