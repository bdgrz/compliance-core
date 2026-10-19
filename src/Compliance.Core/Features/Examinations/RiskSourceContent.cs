using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record RiskSourceContent(DateOnly AsOf, Uuid ProgramId,
    IReadOnlyList<RiskSourceEvent> Events);
