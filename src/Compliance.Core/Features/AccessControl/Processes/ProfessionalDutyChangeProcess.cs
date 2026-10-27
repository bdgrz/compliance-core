using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The ordered professional-duty change process: acquire the exact active staff use (recording only), claim the
///     signed assertion identity permanently, apply the change, then settle the staff use. The abort receiver rejects
///     a change that can never apply. For a revocation the factory returns null for the acquire and settle steps, so
///     the driver skips them: revocation does not require active staff and holds no use.
/// </summary>
static class ProfessionalDutyChangeProcess
{
    public const string Acquire = "acquire";
    public const string Claim = "claim";
    public const string Apply = "apply";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(ProfessionalDutyEffects.ProcessName,
        [nameof(ProfessionalDutyChangeAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizeFirmStaffProfessionalDutyUse)],
                "Recording only; revocation skips this stage."),
            new ProcessStageDefinition(Claim, [nameof(ClaimProfessionalAssertionIdentity)],
                "Permanent; never released."),
            new ProcessStageDefinition(Apply, [nameof(ApplyProfessionalDutyChange)]),
            new ProcessStageDefinition(Settle, [nameof(SettleFirmStaffProfessionalDutyUse)]),
        ], [nameof(RejectProfessionalDutyChange)], Receiver);

    /// <summary>The single frozen target carries the change kind, so a revocation skips the staff stages.</summary>
    static ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var target = ProfessionalDutyTarget.Parse(record.Targets.Single());
        var designation = record.Source.Stream;
        var root = record.Key.RootOperationId;
        var staff = target.IsRecording ? new FirmStaffRecord(target.StaffMemberId).Stream : null;
        return ValueTask.FromResult<IRequest<ReceiverOutcome>?>(step.Receiver switch
        {
            nameof(AuthorizeFirmStaffProfessionalDutyUse) => staff is null
                ? null
                : new AuthorizeFirmStaffProfessionalDutyUse(step.CommandId, record.Source, staff, root,
                    ProfessionalDutyEffects.Generation, null, record.Intent, target.StaffMemberId,
                    target.ContentEventId, target.ContentRevision, target.DesignationId),
            nameof(ClaimProfessionalAssertionIdentity) => new ClaimProfessionalAssertionIdentity(step.CommandId,
                record.Source, new ProfessionalAssertionIdentityClaim(target.AssertionId).Stream, root,
                ProfessionalDutyEffects.Generation, null, record.Intent, target.AssertionId, target.DesignationId,
                target.AssertionSha256, null),
            nameof(ApplyProfessionalDutyChange) => new ApplyProfessionalDutyChange(step.CommandId, record.Source,
                designation, root, ProfessionalDutyEffects.Generation, null, record.Intent, target.DesignationId,
                target.AssertionId, null),
            nameof(SettleFirmStaffProfessionalDutyUse) => staff is null
                ? null
                : new SettleFirmStaffProfessionalDutyUse(step.CommandId, record.Source, staff, root,
                    ProfessionalDutyEffects.Generation, null, record.Intent, target.StaffMemberId,
                    target.DesignationId,
                    record.State == ProcessState.Aborting
                        ? SourceUse.SourceUseSettlementDisposition.Canceled
                        : SourceUse.SourceUseSettlementDisposition.IrreversibleDecision),
            nameof(RejectProfessionalDutyChange) => new RejectProfessionalDutyChange(step.CommandId, root,
                designation, ProfessionalDutyEffects.Generation, 0, record.Intent),
            _ => throw new InvalidOperationException($"{step.Receiver} is not a professional-duty receiver."),
        });
    }
}
