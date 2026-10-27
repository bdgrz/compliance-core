using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.History;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record FindingTypeIBaselineSourceContent(
    DateOnly AsOf,
    Uuid TenantId,
    Uuid ProgramId,
    Uuid FindingId,
    long Revision,
    IReadOnlyList<FindingTypeIBaselineSourceEvent> History);
