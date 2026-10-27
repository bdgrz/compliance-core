using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Content;
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
    public static async ValueTask<Result<TFact>> VerifyAsync<TFact>(IAggregateReader reader, IDomainEventReader events,
        IImmutableContentStore content, Uuid tenantId, Uuid programId,
        Uuid requestId, NativeFactReference source, ImmutableContentRef intent, Uuid operationId, Uuid logicalId,
        Func<TFact, ImmutableContentRef> intentOf, CancellationToken ct) where TFact : DomainEvent, INativeOperationFact
    {
        try
        {
            var parent = await reader.HydrateAsync(new AuditorRequest(tenantId, programId, requestId, logicalId), ct)
                .ConfigureAwait(false);
            var scoped = await AuditorCommands.TermsAsync(content, tenantId, parent, ct).ConfigureAwait(false);
            if (!scoped.IsSuccess)
                return Result<TFact>.Failure(scoped.Error!);
            var history = await AuditorRequestHistory.ReadAsync(events, parent, ct).ConfigureAwait(false);
            if (history.FactById(source.EventId) is not TFact admitted)
                return Fail<TFact>("The admission is not visible on its request yet.", true);
            if (source.Stream != parent.Stream || admitted.Metadata.AggregateVersion != source.AggregateVersion || intentOf(admitted) != intent ||
                admitted.Operation.LogicalId != logicalId || admitted.Operation.RootOperationId != operationId)
                return Fail<TFact>("The source differs from the admission of this child.", false);
            return Result<TFact>.Success(admitted);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Fail<TFact>($"The exact admitted child history cannot be verified: {error.Message}",
                error is ImmutableContentUnavailableException);
        }
    }

    internal static bool HeaderMatches(OperationProvenance operation, DurableIntentHeader header) =>
        header.RootOperationId == operation.RootOperationId && header.FirstOwner == operation.Owner &&
        header.AffectedOwner == operation.AffectedOwner && header.ExpectedRevisionOwner == operation.ExpectedRevisionOwner &&
        header.OriginalActor == operation.Actor && header.Episode == operation.Episode &&
        header.OriginalExpectedRevision == operation.OriginalExpectedRevision &&
        header.AuthoredIntentSha256 == operation.AuthoredIntentSha256 && header.AcceptedAt == operation.AcceptedAt;

    static Result<TFact> Fail<TFact>(string message, bool transient) where TFact : DomainEvent, INativeOperationFact =>
        Result<TFact>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
