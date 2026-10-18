using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Retains independent de-identification review and disposition in the tenant contribution stream.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-reviewed", 2)]
public sealed record MethodologyTemplateContributionReviewed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    MethodologyTemplateContributionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
