using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public interface IAdvisorHandoffDirectoryProjection : IProjectionStore
{
    /// <summary>
    ///     Applies one handoff fact. <paramref name="selection" /> is the verified content selection of a creation or
    ///     selection revision and null for every other fact.
    /// </summary>
    ValueTask ApplyAsync(DomainEvent domainEvent, IReadOnlyList<AdvisorRecordVersion>? selection,
        CancellationToken ct = default);
}
