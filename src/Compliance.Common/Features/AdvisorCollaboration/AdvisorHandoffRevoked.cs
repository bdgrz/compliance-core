using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>The handoff was revoked; feedback admitted before the revocation is still materialized.</summary>
[Discriminator("bdgrz.advisor-handoff.revoked", 1)]
public sealed record AdvisorHandoffRevoked(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    string Rationale, ActorReference RevokedBy, DateTimeOffset RevokedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
