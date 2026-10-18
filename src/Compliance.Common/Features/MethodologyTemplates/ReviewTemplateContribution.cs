using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records a platform review of one tenant-scoped de-identified contribution.</summary>
[Discriminator("bdgrz.methodology-templates.review-template-contribution", 1)]
public sealed record ReviewTemplateContribution(Uuid TenantId, Uuid ContributionId, string Outcome,
    string ReviewNotes) : IRequest, IPlatformOperatorRequest, ICallable;
