using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records the retire methodology template intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.retire-methodology-template", 2)]
public sealed record RetireMethodologyTemplate(Uuid CommandId, Uuid TemplateId, long ExpectedRevision, DateOnly EffectiveOn, string Rationale) : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
