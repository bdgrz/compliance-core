using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The root native acceptance of an engagement acceptance on the engagement owner. It freezes the complete
///     engagement independence intent at <paramref name="Revision" />, the engagement content revision at admission,
///     and pins that content until the acceptance is recorded or rejected. It does not advance the engagement revision
///     and grants no authority by itself.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.service-engagement-acceptance-requested", 1)]
public sealed record ServiceEngagementAcceptanceRequested(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
