using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Services;

/// <summary>Daily identity has no run attempt, current clock time or source revision component.</summary>
public static class DailyReadinessAssessment
{
    public const string ProcessId = "daily-readiness-assessment";

    public static Uuid IdFor(Uuid tenantId, Uuid programId, DateOnly day) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, "daily-readiness:" + programId),
            day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
