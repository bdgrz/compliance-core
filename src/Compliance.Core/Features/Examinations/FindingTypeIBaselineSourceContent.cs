using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record FindingTypeIBaselineSourceContent(
    DateOnly AsOf,
    Uuid TenantId,
    Uuid ProgramId,
    Uuid FindingId,
    long Revision,
    IReadOnlyList<FindingTypeIBaselineSourceEvent> History);
