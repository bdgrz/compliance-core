using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>A compact native MethodologyTemplateDraftReviewed delta; authored bodies and historical versions are immutable content.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-draft-reviewed", 3)]
public sealed record MethodologyTemplateDraftReviewed(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid ActorMemberId, long VersionNumber, Uuid VersionId, string VersionStatus, Uuid AuthorMemberId,
    Uuid? ReviewerMemberId, string ContentSha256, ImmutableContentRef Change, DateTimeOffset OccurredAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IMethodologyTemplateFact;
