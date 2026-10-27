using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The root native acceptance of one positive actual staff assignment on the accepted engagement owner. It freezes
///     the complete engagement independence intent at <paramref name="Revision" /> and pins that content until the
///     assignment is recorded or rejected. It does not advance the engagement revision and grants no authority by
///     itself.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.service-engagement-staff-assignment-requested", 1)]
public sealed record ServiceEngagementStaffAssignmentRequested(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
