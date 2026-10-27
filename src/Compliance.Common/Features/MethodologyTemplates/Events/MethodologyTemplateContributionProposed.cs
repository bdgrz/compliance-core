using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the exact proposed contribution content without copying a retained read snapshot.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-proposed", 3)]
public sealed record MethodologyTemplateContributionProposed(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid ActorMemberId, Uuid ActingUserId, ImmutableContentRef Content, DateTimeOffset OccurredAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
