using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.programs.list-carry-forward-references", 1)]
public sealed record ListCarryForwardReferences(Uuid TenantId, Uuid ProgramId, Uuid ProgramRolloverId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<CarryForwardReferenceView>>, IProgramReadRequest, ICallable;
