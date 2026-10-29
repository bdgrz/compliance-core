using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>An attributable decision that one exact in-period change does not alter the narrative.</summary>
public sealed record SystemDescriptionNoChangeDecisionView(Uuid VersionId, long Version,
    Uuid ChangeId, string Rationale, Uuid DecidedByMemberId, string DecidedByDisplay,
    DateTimeOffset DecidedAt);
