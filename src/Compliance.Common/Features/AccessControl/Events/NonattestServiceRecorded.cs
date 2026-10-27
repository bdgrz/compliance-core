using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
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
