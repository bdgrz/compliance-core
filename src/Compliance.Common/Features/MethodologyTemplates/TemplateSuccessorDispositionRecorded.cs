using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Pins one immutable application change and its original native outcome.</summary>
[Discriminator("bdgrz.methodologytemplates.template-successor-disposition-recorded", 3)]
public sealed record TemplateSuccessorDispositionRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    TemplateApplicationHeader Header, ImmutableContentRef Content, DateTimeOffset OccurredAt,
    OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, ITemplateApplicationFact;
