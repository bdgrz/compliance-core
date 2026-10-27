using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>The client resolved a feedback thread.</summary>
[Discriminator("bdgrz.advisor-feedback.resolved", 1)]
public sealed record AdvisorFeedbackResolved(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId,
    ActorReference ResolvedBy, DateTimeOffset ResolvedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
