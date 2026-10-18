using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Accepts only independently reviewed de-identified content for transfer into platform methodology.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-accepted", 2)]
public sealed record MethodologyTemplateContributionAccepted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    MethodologyTemplateContributionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
