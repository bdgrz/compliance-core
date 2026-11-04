using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Queries;

/// <summary>Reads actual published separate-draft entries without legacy authored-version attribution.</summary>
[Discriminator("bdgrz.methodology-templates.get-methodology-template-version", 2)]
public sealed record GetMethodologyTemplateVersionV2(Uuid TenantId, Uuid ProgramId, Uuid MethodologyTemplateVersionId)
    : IRequest<MethodologyTemplatePublishedVersionView>, IProgramReadRequest, ICallable;
