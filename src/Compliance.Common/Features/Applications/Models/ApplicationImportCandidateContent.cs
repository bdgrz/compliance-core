using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
///     The candidate state one target group produces. <paramref name="Created" /> is true when the group creates the
///     application; <paramref name="Retired" /> when it retires it. A link-only group changes neither.
/// </summary>
public sealed record ApplicationImportCandidateContent(Uuid ApplicationId, bool Created, bool Retired, string? Name,
    string? Purpose, string? OwnerReference, string? RetirementReason, ulong Revision)
{
    /// <summary>Schema of the candidate content.</summary>
    public const string Schema = "import-candidate-content.v1";
}
