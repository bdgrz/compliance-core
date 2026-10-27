using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     One nonattest service appended to the complete history of a client. The complete <see cref="NonattestServiceView" />
///     is immutable content; <paramref name="Revision" /> is the service epoch after this service. The service record
///     identity is <paramref name="OperationId" />.
/// </summary>
[Discriminator("bdgrz.independence.service.recorded", 1)]
public sealed record NonattestServiceRecorded(Uuid TenantId, Uuid OperationId, ImmutableContentRef ServiceContent,
    OperationProvenance Operation, ulong Revision, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
