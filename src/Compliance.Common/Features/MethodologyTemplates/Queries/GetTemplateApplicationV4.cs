using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Queries;

/// <summary>Reads the authoritative current progress of one canonical program/template application.</summary>
[Discriminator("bdgrz.methodology-templates.get-template-application", 4)]
public sealed record GetTemplateApplicationV4(Uuid TenantId, Uuid ProgramId, Uuid TemplateId)
    : IRequest<TemplateApplicationProgressView>, IProgramReadRequest, ICallable;
