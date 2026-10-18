using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Retains proposed reusable content and its source identity only in the contributing tenant stream.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-proposed", 2)]
public sealed record MethodologyTemplateContributionProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    MethodologyTemplateContributionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
