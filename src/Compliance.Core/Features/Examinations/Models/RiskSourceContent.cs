using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.History;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record RiskSourceContent(DateOnly AsOf, Uuid ProgramId,
    IReadOnlyList<RiskSourceEvent> Events);
