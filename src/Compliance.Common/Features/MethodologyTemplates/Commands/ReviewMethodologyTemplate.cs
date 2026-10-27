using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records the review methodology template intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.review-methodology-template", 2)]
public sealed record ReviewMethodologyTemplate(Uuid CommandId, Uuid TemplateId, long Version, string Outcome, string Comments) : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
