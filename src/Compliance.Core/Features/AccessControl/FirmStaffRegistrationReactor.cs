using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Starts <c>FirmStaffRegistrationProcess</c> from a committed registration root fact. The process record exists
///     before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class FirmStaffRegistrationReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(FirmStaffContent.Realm, FirmStaffRecord.Area), WorkloadName),
        IReactorHandler<FirmStaffRegistrationAccepted>
{
    public const string WorkloadName = "FirmStaffRegistrationV1";

    public async ValueTask HandleAsync(IReactorContext<FirmStaffRegistrationAccepted> context, CancellationToken ct)
    {
        FirmStaffRegistrationIntent intent;
        try
        {
            intent = await FirmStaffContent.ReadIntentAsync(content, context.Trigger.Intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen registration cannot be verified: {exception.Message}", false));
        }

        await process.StartAsync(FirmStaffEffects.ProcessName, context, context.Trigger.Intent,
            [intent.CanonicalUserId.ToString()], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
