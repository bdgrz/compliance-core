using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records the approve methodology template version intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.approve-methodology-template-version", 2)]
public sealed record ApproveMethodologyTemplateVersion(Uuid CommandId, Uuid TemplateId, long Version, string ApprovalRationale) : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
