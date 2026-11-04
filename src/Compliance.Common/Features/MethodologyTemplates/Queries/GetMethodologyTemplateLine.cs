using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Queries;

/// <summary>Reads the platform operator's separate-draft line header.</summary>
[Discriminator("bdgrz.methodology-templates.get-methodology-template-line", 1)]
public sealed record GetMethodologyTemplateLine(Uuid TemplateId)
    : IRequest<MethodologyTemplateLineView>, IPlatformOperatorRequest, ICallable;
