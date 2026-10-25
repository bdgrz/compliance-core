using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Starts <c>ReadinessDecisionProcess</c> from a committed readiness or Type I entry decision acceptance. The
///     process record, with the complete canonical gap plan key list of the frozen manifest, exists before the source
///     checkpoint advances; the sweep resumes it if this reactor stops. A manifest that cannot be read yet is
///     transient: no record is created from an unverified decision.
/// </summary>
sealed partial class ReadinessDecisionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ProgramReadiness.Area), WorkloadName),
        IReactorHandler<ReadinessDecisionAccepted>, IReactorHandler<TypeIEntryDecisionAccepted>
{
    public const string WorkloadName = "ReadinessDecisionV1";

    public ValueTask HandleAsync(IReactorContext<ReadinessDecisionAccepted> context, CancellationToken ct) =>
        StartAsync(context, context.Trigger.Intent, ct);

    public ValueTask HandleAsync(IReactorContext<TypeIEntryDecisionAccepted> context, CancellationToken ct) =>
        StartAsync(context, context.Trigger.Intent, ct);

    async ValueTask StartAsync(IReactorContext context, Proofs.ImmutableContentRef intentReference, CancellationToken ct)
    {
        var tenant = Uuid.Parse(context.Source.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<string> targets;
        try
        {
            var intent = await ReadinessContent.ReadAsync<ReadinessDecisionIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
            targets = await ReadinessDecisionProcess.TargetsAsync(content, tenant, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen decision cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(ReadinessEffects.DecisionProcessName, context, intentReference, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
