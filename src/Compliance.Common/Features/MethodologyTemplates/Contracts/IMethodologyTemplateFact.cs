using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;

public interface IMethodologyTemplateFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    long Revision { get; }
    Uuid ActorMemberId { get; }
    long VersionNumber { get; }
    Uuid VersionId { get; }
    string VersionStatus { get; }
    Uuid AuthorMemberId { get; }
    Uuid? ReviewerMemberId { get; }
    string ContentSha256 { get; }
    ImmutableContentRef Change { get; }
    DateTimeOffset OccurredAt { get; }
    ImmutableContentRef? OperationResult { get; }
}
