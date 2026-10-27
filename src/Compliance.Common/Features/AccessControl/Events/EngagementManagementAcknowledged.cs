using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The immutable personal management acknowledgement of one engagement, materialized from the authorized personal
///     decision. The complete <see cref="EngagementManagementAcknowledgementView" /> is immutable content; the
///     acknowledgement identity is <paramref name="OperationId" />.
/// </summary>
[Discriminator("bdgrz.service-engagement.management-acknowledged", 1)]
public sealed record EngagementManagementAcknowledged(Uuid TenantId, Uuid OperationId,
    ImmutableContentRef AcknowledgementContent, OperationProvenance Operation, ulong Revision,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
