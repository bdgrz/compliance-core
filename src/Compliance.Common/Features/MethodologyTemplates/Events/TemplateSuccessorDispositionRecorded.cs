using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>Pins one immutable application change and its original native outcome.</summary>
[Discriminator("bdgrz.methodologytemplates.template-successor-disposition-recorded", 3)]
public sealed record TemplateSuccessorDispositionRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    TemplateApplicationHeader Header, ImmutableContentRef Content, DateTimeOffset OccurredAt,
    OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, ITemplateApplicationFact;
