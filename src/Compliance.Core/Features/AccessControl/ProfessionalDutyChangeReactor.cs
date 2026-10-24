using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Starts <c>ProfessionalDutyChangeProcess</c> from a committed change root fact. The process record, with its
///     frozen target, exists before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class ProfessionalDutyChangeReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(ProfessionalDutyContent.Realm,
            FirmProfessionalDutyDesignation.Area), WorkloadName),
        IReactorHandler<ProfessionalDutyChangeAccepted>
{
    public const string WorkloadName = "ProfessionalDutyChangeV1";

    public async ValueTask HandleAsync(IReactorContext<ProfessionalDutyChangeAccepted> context, CancellationToken ct)
    {
        ProfessionalDutyChangeIntent intent;
        VerifiedFirmProfessionalDutyAssertion evidence;
        try
        {
            intent = await ProfessionalDutyContent.ReadIntentAsync(content, context.Trigger.Intent, ct)
                .ConfigureAwait(false);
            evidence = await ProfessionalDutyContent.ReadVerifiedScopeAsync(content, intent.VerifiedScope, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen change cannot be verified: {exception.Message}", false));
        }

        var record = await process.StartAsync(ProfessionalDutyEffects.ProcessName, context, context.Trigger.Intent,
            [ProfessionalDutyTarget.For(intent, evidence).ToString()], $"reactor:{WorkloadName}", ct)
            .ConfigureAwait(false);
        // A process is keyed by the signed assertion identity. A record that belongs to another accepted change of
        // the same identity means this change can never be claimed: reject it so it does not stay pending.
        if (record.Source.EventId == context.Trigger.Metadata.EventId)
            return;
        var rejected = await bus.SendAsync(new RejectProfessionalDutyChange(ProfessionalDutyEffects.ReceiverCommandId(
            context.Trigger.Metadata.EventId, nameof(RejectProfessionalDutyChange), null, intent.Header.Episode),
            intent.VerifiedAssertionId, context.Source.Stream, ProfessionalDutyEffects.Generation, 0,
            context.Trigger.Intent), context, ct).ConfigureAwait(false);
        if (!rejected.IsSuccess)
            throw new ReactionCommandFailedException(rejected.Error!);
    }
}
