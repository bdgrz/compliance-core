using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

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
