using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A requested acceptance was rejected before the irreversible authorization. The rejection fences the pending
///     acceptance against late delivery and releases the engagement content pin; no permanent practice commitment was
///     ever made for it.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.service-engagement-acceptance-rejected", 1)]
public sealed record ServiceEngagementAcceptanceRejected(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
