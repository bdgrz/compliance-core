using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.History;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record ProviderSourceContent(DateOnly AsOf, Uuid ProgramId,
    IReadOnlyList<ProviderSourceEvent> Events);
