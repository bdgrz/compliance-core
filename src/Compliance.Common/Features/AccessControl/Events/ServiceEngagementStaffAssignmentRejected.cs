using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A requested positive staff assignment was rejected before the irreversible authorization. The rejection fences
///     the pending assignment against late delivery; the engagement and every earlier permanent wall are retained.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.service-engagement-staff-assignment-rejected", 1)]
public sealed record ServiceEngagementStaffAssignmentRejected(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
