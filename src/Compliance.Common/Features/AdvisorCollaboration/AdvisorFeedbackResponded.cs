using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>The client responded on a feedback thread.</summary>
[Discriminator("bdgrz.advisor-feedback.responded", 1)]
public sealed record AdvisorFeedbackResponded(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId,
    string Response, ActorReference RespondedBy, DateTimeOffset RespondedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
