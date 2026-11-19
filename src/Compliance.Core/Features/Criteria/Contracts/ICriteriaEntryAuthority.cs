using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Contracts;

/// <summary>Resolves one entry against exact, retained publication and participant facts.</summary>
/// <remarks>
/// A read-model row is not an authority. <see cref="CriteriaEntryAuthorityStatus.NotMember" />
/// is valid only when source coverage proves exclusion; an unobserved outcome is pending.
/// </remarks>
public interface ICriteriaEntryAuthority
{
    /// <summary>Reads one published entry's source-backed outcome and proof.</summary>
    ValueTask<Result<CriteriaEntryAuthorityResult>> ReadAsync(Uuid editionId, string identifier,
        CancellationToken ct = default);
}
