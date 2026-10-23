using System.Globalization;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The ordered registration process: claim the canonical user permanently, then complete the staff record. The
///     abort receiver rejects a registration whose canonical user another staff record owns.
/// </summary>
static class FirmStaffRegistrationProcess
{
    public const string Claim = "claim";
    public const string Complete = "complete";

    public static ProcessDefinition Definition() => new(FirmStaffEffects.ProcessName,
        [nameof(FirmStaffRegistrationAccepted)], true,
        [
            new ProcessStageDefinition(Claim, [nameof(ClaimFirmStaffIdentity)], "Permanent canonical-user claim."),
            new ProcessStageDefinition(Complete, [nameof(CompleteFirmStaffRegistration)]),
        ], [nameof(RejectFirmStaffRegistration)], Receiver);

    /// <summary>The canonical user is the single claim target; the staff member is the record stream's resource.</summary>
    static ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var staffMemberId = Uuid.Parse(record.Source.Stream.Resource, CultureInfo.InvariantCulture);
        var userId = Uuid.Parse(record.Targets.Single(), CultureInfo.InvariantCulture);
        var recordStream = new FirmStaffRecord(staffMemberId).Stream;
        return ValueTask.FromResult<IRequest<ReceiverOutcome>?>(step.Receiver switch
        {
            nameof(ClaimFirmStaffIdentity) => new ClaimFirmStaffIdentity(step.CommandId, record.Source,
                new FirmStaffIdentityClaim(userId).Stream, record.Key.RootOperationId, FirmStaffEffects.Generation,
                null, record.Intent, staffMemberId, userId),
            nameof(CompleteFirmStaffRegistration) => new CompleteFirmStaffRegistration(step.CommandId, record.Source,
                recordStream, record.Key.RootOperationId, FirmStaffEffects.Generation, null, record.Intent,
                staffMemberId, userId),
            nameof(RejectFirmStaffRegistration) => new RejectFirmStaffRegistration(step.CommandId,
                record.Key.RootOperationId, recordStream, FirmStaffEffects.Generation, 0, record.Intent),
            _ => throw new InvalidOperationException($"{step.Receiver} is not a registration receiver."),
        });
    }
}
