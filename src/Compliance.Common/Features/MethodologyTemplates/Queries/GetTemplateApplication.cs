using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.methodology-templates.get-template-application", 1)]
public sealed record GetTemplateApplication(Uuid TenantId, Uuid ProgramId, Uuid TemplateApplicationId)
    : IRequest<TemplateApplicationView>, IProgramReadRequest, ICallable;
