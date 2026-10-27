using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>The client responded on a feedback thread.</summary>
[Discriminator("bdgrz.advisor-feedback.responded", 2)]
public sealed record AdvisorFeedbackResponded(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId,
    ImmutableContentRef Authored, ActorReference RespondedBy, DateTimeOffset RespondedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
