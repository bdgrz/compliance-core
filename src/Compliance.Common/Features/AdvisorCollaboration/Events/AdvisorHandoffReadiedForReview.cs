using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>The handoff was opened to the assigned advisor for review.</summary>
[Discriminator("bdgrz.advisor-handoff.readied-for-review", 1)]
public sealed record AdvisorHandoffReadiedForReview(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    ActorReference ReadiedBy, DateTimeOffset ReadiedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
