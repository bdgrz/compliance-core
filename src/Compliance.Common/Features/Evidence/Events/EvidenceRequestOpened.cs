using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>
///     Opens a request for one piece of evidence, owned by a member and due on a date. It is the native acceptance of
///     <see cref="OpenEvidenceRequest" /> and the request's revision one.
/// </summary>
[Discriminator("bdgrz.evidence.request.opened", 2)]
public sealed record EvidenceRequestOpened(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    string Title, string Instructions, Uuid OwnerMemberId, DateOnly DueOn, Uuid? ControlId,
    ActorReference RequestedBy, DateTimeOffset OpenedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
