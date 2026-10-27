using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;

public interface ITemplateApplicationFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    long Revision { get; }
    Uuid? ActorMemberId { get; }
    TemplateApplicationHeader Header { get; }
    ImmutableContentRef Content { get; }
    DateTimeOffset OccurredAt { get; }
    ImmutableContentRef OperationResult { get; }
}
