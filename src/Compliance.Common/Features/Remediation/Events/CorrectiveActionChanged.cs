using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>An open corrective action's description, owner and due date were changed.</summary>
[Discriminator("bdgrz.finding.corrective_action.changed", 1)]
public sealed record CorrectiveActionChanged(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ActionId, string Description, Uuid OwnerMemberId, DateOnly DueOn,
    ActorReference ChangedBy, DateTimeOffset ChangedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
