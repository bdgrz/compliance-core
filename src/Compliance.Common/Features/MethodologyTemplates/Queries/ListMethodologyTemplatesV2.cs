using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Queries;

/// <summary>Lists separate-draft templates with an actual published version for the authorized program.</summary>
[Discriminator("bdgrz.methodology-templates.list-methodology-templates", 2)]
public sealed record ListMethodologyTemplatesV2(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<MethodologyTemplateSummaryView>>, IProgramReadRequest, ICallable;
