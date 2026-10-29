using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>The handoff was revoked; feedback admitted before the revocation is still materialized.</summary>
[Discriminator("bdgrz.advisor-handoff.revoked", 2)]
public sealed record AdvisorHandoffRevoked(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    ImmutableContentRef Authored, ActorReference RevokedBy, DateTimeOffset RevokedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
