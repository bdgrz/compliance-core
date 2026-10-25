using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Starts <c>FirmStaffRegistrationProcess</c> from a committed registration root fact. The process record exists
///     before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class FirmStaffRegistrationReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, ISourceFactResolver sources, IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(FirmStaffContent.Realm, FirmStaffRecord.Area), WorkloadName),
        IReactorHandler<FirmStaffRegistrationAccepted>
{
    public const string WorkloadName = "FirmStaffRegistrationV1";

    public async ValueTask HandleAsync(IReactorContext<FirmStaffRegistrationAccepted> context, CancellationToken ct)
    {
        await FirmStaffSource.VerifyAsync(sources, content, context.Source.Stream, context.Source.ResourceOffset,
            context.Trigger, ct).ConfigureAwait(false);
        var owner = await reader.HydrateAsync(new FirmStaffRecord(context.Trigger.Operation.RootOperationId), ct)
            .ConfigureAwait(false);
        if (owner.Accepted != context.Trigger || owner.Accepted.Metadata != context.Trigger.Metadata)
            throw new SourceFactVerificationException("The registration source differs from its authoritative root admission.");
        FirmStaffRegistrationIntent intent;
        try
        {
            intent = await FirmStaffContent.ReadIntentAsync(content, context.Trigger.Intent, ct).ConfigureAwait(false);
            FirmStaffContent.RequireIntent(owner, intent);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen registration cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        await process.StartAsync(FirmStaffEffects.ProcessName, context, context.Trigger.Intent,
            [intent.CanonicalUserId.ToString()], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
