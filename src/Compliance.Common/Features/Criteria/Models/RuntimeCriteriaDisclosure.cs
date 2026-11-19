using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One independently recorded support or coverage disclosure.</summary>
public sealed record RuntimeCriteriaDisclosure(Uuid EditionId, string Category, string Code, string Note);
