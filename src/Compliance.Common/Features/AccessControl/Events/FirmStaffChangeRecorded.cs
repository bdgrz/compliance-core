using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     One change of one staff record: its registration completing or a status change. The complete
///     <see cref="FirmStaffMemberView" /> after the change is immutable content; no cumulative directory is carried.
///     <paramref name="Intent" /> is always the record's frozen registration intent.
/// </summary>
[Discriminator("bdgrz.firm-staff.changed", 1)]
public sealed record FirmStaffChangeRecorded(Uuid OperationId, string ChangeKind, string? Reason,
    ImmutableContentRef StaffContent, OperationProvenance Operation, ulong Revision, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
