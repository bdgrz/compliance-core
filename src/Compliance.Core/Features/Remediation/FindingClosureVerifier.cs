using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Verifies a receiver's proven source: the frozen closure intent and authored decision are read from verified
///     immutable content, and the accepted closure fact (or the terminal fact that ended it) is found on the
///     authoritative finding. The source reference only names the fact; nothing the caller states is trusted until the
///     finding confirms it.
/// </summary>
sealed class FindingClosureVerifier(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
{
    /// <summary>Hydrates an owner from the authoritative streams.</summary>
    public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct)
        where TAggregate : Aggregate => reader.HydrateAsync(aggregate, ct);

    /// <summary>Verifies the accepted closure named by <paramref name="source" />.</summary>
    public async ValueTask<FindingClosure> VerifyAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var closure = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return closure;
        var finding = closure.Finding!;
        if (finding.AcceptedClosure(operationId) is not { } accepted)
            return FindingClosure.Failed(Transient("The accepted closure is not visible yet."));
        return source.Stream != finding.Stream || accepted.Metadata.EventId != source.EventId ||
               accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference ||
               accepted.Generation != (long)closure.Intent!.ParentGeneration
            ? FindingClosure.Failed(Permanent("The source is not the accepted closure of this finding."))
            : closure;
    }

    /// <summary>Verifies the terminal fact (closed or canceled) named by <paramref name="source" />.</summary>
    public async ValueTask<FindingClosure> VerifyTerminalAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var closure = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return closure;
        var finding = closure.Finding!;
        if (finding.TerminalClosure(operationId) is not { } terminal)
            return FindingClosure.Failed(Transient("Neither the closure nor its cancellation is visible yet."));
        var intent = terminal switch
        {
            FindingClosedV2 closed => closed.Intent,
            FindingClosureCanceled canceled => canceled.Intent,
            _ => null,
        };
        return source.Stream != finding.Stream || terminal.Metadata.EventId != source.EventId ||
               terminal.Metadata.AggregateVersion != source.AggregateVersion || intent != intentReference
            ? FindingClosure.Failed(Permanent("The source is not the terminal fact of this closure."))
            : closure;
    }

    async ValueTask<FindingClosure> ReadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (!Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenant))
            return FindingClosure.Failed(Permanent("The closure source is not a tenant finding stream."));
        ClosureIntent intent;
        ClosureDecision decision;
        try
        {
            intent = await FindingContent.ReadAsync<ClosureIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
            decision = await FindingContent.ReadAsync<ClosureDecision>(content, tenant, intent.AuthoredDecision, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return FindingClosure.Failed(Transient($"The frozen closure cannot be verified: {exception.Message}"));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return FindingClosure.Failed(Permanent($"The frozen closure cannot be verified: {exception.Message}"));
        }

        if (intent.Header.RootOperationId != operationId || intent.ParentKind != ClosureParentKind.Finding ||
            intent.ParentId == Uuid.Empty || decision.ProgramId == Uuid.Empty || decision.DecisionId != operationId)
            return FindingClosure.Failed(Permanent("The command differs from the frozen closure intent."));
        var finding = await reader.HydrateAsync(new FindingAggregate(tenant, decision.ProgramId, intent.ParentId,
            commandId, operationId), ct).ConfigureAwait(false);
        try
        {
            var history = await FindingHistory.ReadAsync(events, finding, ct).ConfigureAwait(false);
            return new FindingClosure(null, finding, intent, decision, tenant, history);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return FindingClosure.Failed(new RequestError(RequestErrorKind.Conflict,
                $"The exact finding source cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
