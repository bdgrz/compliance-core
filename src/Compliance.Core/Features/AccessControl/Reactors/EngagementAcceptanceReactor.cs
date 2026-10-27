using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Starts <c>EngagementAcceptanceProcess</c> from a committed acceptance or staff assignment admission. The process
///     record, with its frozen intent and the canonical acquisition keys of its frozen manifests, exists before the
///     source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class EngagementAcceptanceReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ServiceEngagementIndependence.Area), WorkloadName),
        IReactorHandler<ServiceEngagementAcceptanceRequested>,
        IReactorHandler<ServiceEngagementStaffAssignmentRequested>
{
    public const string WorkloadName = "EngagementAcceptanceV1";

    public ValueTask HandleAsync(IReactorContext<ServiceEngagementAcceptanceRequested> context, CancellationToken ct) =>
        VerifyAndStartAsync(context, context.Trigger, context.Trigger.Intent, ct);

    public ValueTask HandleAsync(IReactorContext<ServiceEngagementStaffAssignmentRequested> context,
        CancellationToken ct) => StartAsync(context, context.Trigger.Intent, ct);

    async ValueTask VerifyAndStartAsync<T>(IReactorContext context, T trigger, ImmutableContentRef intent, CancellationToken ct)
        where T : DomainEvent, Bdgrz.Compliance.Features.NativeOperations.INativeOperationFact
    {
        await EngagementNativeSource.VerifyAsync(sources, content, context.Source.Stream,
            context.Source.ResourceOffset, trigger, ct).ConfigureAwait(false);
        await StartAsync(context, intent, ct).ConfigureAwait(false);
    }

    async ValueTask StartAsync(IReactorContext context, ImmutableContentRef intentReference, CancellationToken ct)
    {
        IReadOnlyList<string> targets;
        try
        {
            var realm = context.Source.Stream.Realm;
            var intent = await content.ReadJsonAsync<EngagementIndependenceIntent>(realm, intentReference, ct)
                .ConfigureAwait(false);
            targets = EngagementAcceptanceProcess.Targets(
                await content.ReadSelectionAsync<SourceUseRequirement>(realm, intent.SourceUses, ct)
                    .ConfigureAwait(false),
                await content.ReadSelectionAsync<PracticeSubjectIntent>(realm, intent.Subjects, ct)
                    .ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen acceptance cannot be read yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(EngagementAcceptanceEffects.ProcessName, context, intentReference, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
