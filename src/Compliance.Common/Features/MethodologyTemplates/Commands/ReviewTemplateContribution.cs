using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records a platform review of one tenant-scoped de-identified contribution.</summary>
[Discriminator("bdgrz.methodology-templates.review-template-contribution", 2)]
public sealed record ReviewTemplateContribution(Uuid CommandId, Uuid TenantId, Uuid ContributionId, string Outcome,
    string ReviewNotes) : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
