using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Processes;

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
    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var staffMemberId = Uuid.Parse(record.Source.Stream.Resource, CultureInfo.InvariantCulture);
        var userId = Uuid.Parse(record.Targets.Single(), CultureInfo.InvariantCulture);
        var recordStream = new FirmStaffRecord(staffMemberId).Stream;
        var practice = step.Receiver == nameof(ClaimFirmStaffIdentity)
            ? (await services.GetRequiredService<IImmutableContentStore>().ReadJsonAsync<FirmStaffRegistrationIntent>(
                record.Key.Realm, record.Intent, ct).ConfigureAwait(false)).Practice
            : null;
        return step.Receiver switch
        {
            nameof(ClaimFirmStaffIdentity) => new ClaimFirmStaffIdentity(step.CommandId, record.Source,
                new FirmStaffIdentityClaim(userId).Stream, record.Key.RootOperationId, FirmStaffEffects.Generation,
                null, record.Intent, staffMemberId, userId, practice!),
            nameof(CompleteFirmStaffRegistration) => new CompleteFirmStaffRegistration(step.CommandId, record.Source,
                recordStream, record.Key.RootOperationId, FirmStaffEffects.Generation, null, record.Intent,
                staffMemberId, userId),
            nameof(RejectFirmStaffRegistration) => new RejectFirmStaffRegistration(step.CommandId,
                record.Key.RootOperationId, recordStream, FirmStaffEffects.Generation, 0, record.Intent),
            _ => throw new InvalidOperationException($"{step.Receiver} is not a registration receiver."),
        };
    }
}
