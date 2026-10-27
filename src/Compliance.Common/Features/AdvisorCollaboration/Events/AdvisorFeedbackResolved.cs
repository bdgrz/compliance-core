using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>The client resolved a feedback thread.</summary>
[Discriminator("bdgrz.advisor-feedback.resolved", 1)]
public sealed record AdvisorFeedbackResolved(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId,
    ActorReference ResolvedBy, DateTimeOffset ResolvedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
