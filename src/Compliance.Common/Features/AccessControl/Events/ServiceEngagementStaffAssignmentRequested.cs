using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
