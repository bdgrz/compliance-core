using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>The handoff was closed; feedback admitted before the closure is still materialized.</summary>
[Discriminator("bdgrz.advisor-handoff.closed", 1)]
public sealed record AdvisorHandoffClosed(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    string Rationale, ActorReference ClosedBy, DateTimeOffset ClosedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
