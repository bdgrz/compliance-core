using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>The handoff was closed; feedback admitted before the closure is still materialized.</summary>
[Discriminator("bdgrz.advisor-handoff.closed", 2)]
public sealed record AdvisorHandoffClosed(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    ImmutableContentRef Authored, ActorReference ClosedBy, DateTimeOffset ClosedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
