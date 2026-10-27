using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>Records the exact proposed contribution content without copying a retained read snapshot.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-proposed", 3)]
public sealed record MethodologyTemplateContributionProposed(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid ActorMemberId, Uuid ActingUserId, ImmutableContentRef Content, DateTimeOffset OccurredAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
