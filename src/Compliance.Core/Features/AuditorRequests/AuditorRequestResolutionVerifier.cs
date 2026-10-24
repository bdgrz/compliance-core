using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Verifies a receiver's proven source: the frozen resolution intent and authored decision are read from verified
///     immutable content, and the accepted resolution fact (or the terminal fact that ended it) is found on the
///     authoritative request. The source reference only names the fact; nothing the caller states is trusted until the
///     request confirms it.
/// </summary>
sealed class AuditorRequestResolutionVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>Hydrates an owner from the authoritative streams.</summary>
    public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct)
        where TAggregate : Aggregate => reader.HydrateAsync(aggregate, ct);

    /// <summary>Verifies the accepted resolution named by <paramref name="source" />.</summary>
    public async ValueTask<AuditorRequestResolution> VerifyAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var resolution = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (resolution.Error is not null)
            return resolution;
        var request = resolution.Request!;
        if (request.AcceptedResolution(operationId) is not { } accepted)
            return AuditorRequestResolution.Failed(Transient("The accepted resolution is not visible yet."));
        return source.Stream != request.Stream || accepted.Metadata.EventId != source.EventId ||
               accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference ||
               accepted.Generation != (long)resolution.Intent!.ParentGeneration
            ? AuditorRequestResolution.Failed(Permanent("The source is not the accepted resolution of this request."))
            : resolution;
    }

    /// <summary>Verifies the terminal fact (resolved or canceled) named by <paramref name="source" />.</summary>
    public async ValueTask<AuditorRequestResolution> VerifyTerminalAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var resolution = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (resolution.Error is not null)
            return resolution;
        var request = resolution.Request!;
        if (request.TerminalResolution(operationId) is not { } terminal)
            return AuditorRequestResolution.Failed(Transient("Neither the resolution nor its cancellation is visible yet."));
        var intent = terminal switch
        {
            AuditorRequestResolved resolved => resolved.Change,
            AuditorRequestResolutionCanceled canceled => canceled.Intent,
            _ => null,
        };
        return source.Stream != request.Stream || terminal.Metadata.EventId != source.EventId ||
               terminal.Metadata.AggregateVersion != source.AggregateVersion || intent != intentReference
            ? AuditorRequestResolution.Failed(Permanent("The source is not the terminal fact of this resolution."))
            : resolution;
    }

    async ValueTask<AuditorRequestResolution> ReadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (!Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenant))
            return AuditorRequestResolution.Failed(Permanent("The resolution source is not a tenant request stream."));
        ClosureIntent intent;
        AuditorResolutionDecision decision;
        try
        {
            intent = await AuditorContent.ReadAsync<ClosureIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
            decision = await AuditorContent.ReadAsync<AuditorResolutionDecision>(content, tenant, intent.AuthoredDecision,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return AuditorRequestResolution.Failed(Transient($"The frozen resolution cannot be verified: {exception.Message}"));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return AuditorRequestResolution.Failed(Permanent($"The frozen resolution cannot be verified: {exception.Message}"));
        }

        if (intent.Header.RootOperationId != operationId || intent.ParentKind != ClosureParentKind.AuditorRequest ||
            intent.ParentId == Uuid.Empty || decision.ProgramId == Uuid.Empty || decision.DecisionId != operationId)
            return AuditorRequestResolution.Failed(Permanent("The command differs from the frozen resolution intent."));
        var request = await reader.HydrateAsync(new AuditorRequest(tenant, decision.ProgramId, intent.ParentId, commandId),
            ct).ConfigureAwait(false);
        return new AuditorRequestResolution(null, request, intent, decision, tenant);
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
