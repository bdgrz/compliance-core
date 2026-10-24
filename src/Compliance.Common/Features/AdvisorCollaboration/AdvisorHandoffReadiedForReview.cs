using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>The handoff was opened to the assigned advisor for review.</summary>
[Discriminator("bdgrz.advisor-handoff.readied-for-review", 1)]
public sealed record AdvisorHandoffReadiedForReview(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    ActorReference ReadiedBy, DateTimeOffset ReadiedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
