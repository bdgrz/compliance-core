using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Queries;

[Discriminator("bdgrz.readiness.type_i_entry.list", 2)]
public sealed record ListTypeIEntryDecisionsV2(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<TypeIEntryDecisionRecord>>, IProgramReadRequest, ICallable;
