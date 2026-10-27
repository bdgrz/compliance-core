using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>A required change with a stable source identity and due date before or during Type II.</summary>
public sealed record TypeIIPlannedChange(Uuid ChangeId, string Description, DateOnly RequiredBy);
