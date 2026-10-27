using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Verifies a receiver's proven source: the frozen run or decision intent is read from verified immutable content,
///     and the accepted fact (or the terminal fact that ended it) is found on the authoritative program readiness. The
///     source reference only names the fact; nothing the caller states is trusted until the owner confirms it.
/// </summary>
sealed class ReadinessVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>Hydrates an owner from the authoritative streams.</summary>
    public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct)
        where TAggregate : Aggregate => reader.HydrateAsync(aggregate, ct);

    /// <summary>Verifies the admitted run named by <paramref name="source" />.</summary>
    public async ValueTask<ReadinessRun> VerifyRunAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (!Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenant))
            return ReadinessRun.Failed(Permanent("The run source is not a tenant program readiness stream."));
        ReadinessRunIntent intent;
        try
        {
            intent = await ReadinessContent.ReadAsync<ReadinessRunIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return ReadinessRun.Failed(new RequestError(RequestErrorKind.Conflict,
                $"The frozen run cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (intent.Header.RootOperationId != operationId || intent.AssessmentId != operationId ||
            intent.ProgramId == Uuid.Empty)
            return ReadinessRun.Failed(Permanent("The command differs from the frozen run intent."));
        var program = await reader.HydrateAsync(new ProgramReadiness(tenant, intent.ProgramId, commandId, operationId), ct)
            .ConfigureAwait(false);
        if (program.RequestedRun(operationId) is not { } requested)
            return ReadinessRun.Failed(Transient("The admitted run is not visible yet."));
        return source.Stream != program.Stream || requested.Metadata.EventId != source.EventId ||
               requested.Metadata.AggregateVersion != source.AggregateVersion || requested.Intent != intentReference ||
               requested.Generation != (long)intent.AdoptionGeneration ||
               !HeaderMatches(intent.Header, requested.Operation)
            ? ReadinessRun.Failed(Permanent("The source is not the admitted run of this program."))
            : new ReadinessRun(null, program, requested, intent, tenant);
    }

    /// <summary>Verifies the accepted decision named by <paramref name="source" />.</summary>
    public async ValueTask<ReadinessDecision> VerifyAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var decision = await ReadDecisionAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (decision.Error is not null)
            return decision;
        var program = decision.Program!;
        if (program.AcceptedDecision(operationId) is not { } accepted)
            return ReadinessDecision.Failed(Transient("The accepted decision is not visible yet."));
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        return source.Stream != program.Stream || accepted.Metadata!.EventId != source.EventId ||
               accepted.Metadata.AggregateVersion != source.AggregateVersion || terms.Intent != intentReference ||
               terms.Kind != decision.Intent!.DecisionKind ||
               !HeaderMatches(decision.Intent.Header, ((INativeOperationFact)accepted).Operation)
            ? ReadinessDecision.Failed(Permanent("The source is not the accepted decision of this program."))
            : decision with { Fact = accepted, Generation = terms.Generation };
    }

    /// <summary>Verifies the terminal fact (recorded or canceled) named by <paramref name="source" />.</summary>
    public async ValueTask<ReadinessDecision> VerifyTerminalAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var decision = await ReadDecisionAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (decision.Error is not null)
            return decision;
        var program = decision.Program!;
        if (program.TerminalDecision(operationId) is not { } terminal)
            return ReadinessDecision.Failed(Transient("Neither the decision nor its cancellation is visible yet."));
        var intent = terminal switch
        {
            ReadinessDecisionRecorded recorded => recorded.Intent,
            TypeIEntryDecisionRecorded recorded => recorded.Intent,
            ReadinessDecisionCanceled canceled => canceled.Intent,
            _ => null,
        };
        if (program.AcceptedDecision(operationId) is not INativeOperationFact admission)
            return ReadinessDecision.Failed(Transient("The original accepted decision is not visible yet."));
        var terms = ProgramReadiness.DecisionTerms((DomainEvent)admission)!.Value;
        var generation = terms.Generation;
        return terms.Intent != intentReference || terms.Kind != decision.Intent!.DecisionKind ||
               !HeaderMatches(decision.Intent.Header, admission.Operation) || source.Stream != program.Stream || terminal.Metadata!.EventId != source.EventId ||
               terminal.Metadata.AggregateVersion != source.AggregateVersion || intent != intentReference
            ? ReadinessDecision.Failed(Permanent("The source is not the terminal fact of this decision."))
            : decision with { Fact = terminal, Generation = generation };
    }

    async ValueTask<ReadinessDecision> ReadDecisionAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (!Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenant))
            return ReadinessDecision.Failed(Permanent("The decision source is not a tenant program readiness stream."));
        ReadinessDecisionIntent intent;
        try
        {
            intent = await ReadinessContent.ReadAsync<ReadinessDecisionIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return ReadinessDecision.Failed(new RequestError(RequestErrorKind.Conflict,
                $"The frozen decision cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (intent.Header.RootOperationId != operationId || intent.ProgramId == Uuid.Empty ||
            intent.AssessmentId == Uuid.Empty)
            return ReadinessDecision.Failed(Permanent("The command differs from the frozen decision intent."));
        var program = await reader.HydrateAsync(new ProgramReadiness(tenant, intent.ProgramId, commandId, operationId), ct)
            .ConfigureAwait(false);
        return new ReadinessDecision(null, program, null, intent, 0, tenant);
    }

    internal static bool HeaderMatches(DurableIntentHeader header, OperationProvenance operation) =>
        header.RootOperationId == operation.RootOperationId && header.FirstOwner == (operation.FirstOwner ?? operation.Owner) &&
        header.AffectedOwner == operation.AffectedOwner && header.ExpectedRevisionOwner == operation.ExpectedRevisionOwner &&
        header.OriginalActor == operation.Actor && header.Episode == operation.Episode &&
        header.OriginalExpectedRevision == operation.OriginalExpectedRevision &&
        header.AuthoredIntentSha256 == operation.AuthoredIntentSha256 && header.AcceptedAt == operation.AcceptedAt;

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}

/// <summary>
///     A verified run: the authoritative program readiness, its admitted run fact and the frozen run intent. Exactly one
///     of <paramref name="Error" /> and the verified parts is set.
/// </summary>
sealed record ReadinessRun(RequestError? Error, ProgramReadiness? Program, ReadinessAssessmentRequested? Requested,
    ReadinessRunIntent? Intent, Uuid TenantId)
{
    /// <summary>A failed verification.</summary>
    public static ReadinessRun Failed(RequestError error) => new(error, null, null, null, Uuid.Empty);
}

/// <summary>
///     A verified decision: the authoritative program readiness, the accepted or terminal fact and the frozen decision
///     intent. <paramref name="Generation" /> is the adoption generation the decision sealed.
/// </summary>
sealed record ReadinessDecision(RequestError? Error, ProgramReadiness? Program, DomainEvent? Fact,
    ReadinessDecisionIntent? Intent, long Generation, Uuid TenantId)
{
    /// <summary>A failed verification.</summary>
    public static ReadinessDecision Failed(RequestError error) => new(error, null, null, null, 0, Uuid.Empty);
}
