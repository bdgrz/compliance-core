using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record ProviderSourceContent(DateOnly AsOf, Uuid ProgramId,
    IReadOnlyList<ProviderSourceEvent> Events);
