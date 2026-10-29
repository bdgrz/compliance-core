using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.system-description.list-system-description-versions", 1)]
public sealed record ListSystemDescriptionVersions(Uuid TenantId, Uuid ProgramId, Uuid SystemDescriptionId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<SystemDescriptionVersionView>>, IProgramReadRequest, ICallable;
