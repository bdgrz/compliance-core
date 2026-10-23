using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Ends a pending professional-duty change that can definitely never apply: the change is rejected first, which
///     fences claim and apply against late delivery, and only then is the staff use settled as canceled, even when it
///     was never acquired, so a delayed acquisition is refused. Both effects use the deterministic command ids the
///     process driver would use, so a later driver step replays them. Nothing is inferred from missing native state.
/// </summary>
sealed class ProfessionalDutyFences(IRequestBus bus)
{
    public async ValueTask<Result<ReceiverOutcome>> RejectAndFenceAsync(ProfessionalDutyChange change,
        NativeFactReference source, ImmutableContentRef intentReference, string reason, IExecutionContext context,
        CancellationToken ct)
    {
        var (owner, intent, evidence) = (change.Owner!, change.Intent!, change.Evidence!);
        var assertionId = intent.VerifiedAssertionId;
        var episode = intent.Header.Episode;
        var rejectionId = ProfessionalDutyEffects.ReceiverCommandId(source.EventId,
            nameof(RejectProfessionalDutyChange), null, episode);
        var rejected = await bus.SendAsync(new RejectProfessionalDutyChange(rejectionId, assertionId, owner.Stream,
            ProfessionalDutyEffects.Generation, 0, intentReference), context, ct).ConfigureAwait(false);
        if (!rejected.IsSuccess)
            return rejected;
        var evidenceFact = rejected.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            ReceiverOutcome.Superseded superseded => superseded.Evidence,
            _ => null,
        };
        // A change that already applied recovers forward: its own settlement releases the use.
        if (evidenceFact is null)
            return rejected;
        if (intent.StaffEligibility is { } requirement)
        {
            var target = ProfessionalDutyTarget.For(intent, evidence);
            var settlementId = ProfessionalDutyEffects.ReceiverCommandId(source.EventId,
                nameof(SettleFirmStaffProfessionalDutyUse), target.ToString(), episode);
            var settled = await bus.SendAsync(new SettleFirmStaffProfessionalDutyUse(settlementId, source,
                requirement.SourceOwner, assertionId, ProfessionalDutyEffects.Generation, null, intentReference,
                target.StaffMemberId, intent.DesignationId, SourceUseSettlementDisposition.Canceled), context, ct)
                .ConfigureAwait(false);
            if (!settled.IsSuccess)
                return settled;
        }

        return SourceUseReceiverSupport.Superseded(reason, evidenceFact);
    }
}
