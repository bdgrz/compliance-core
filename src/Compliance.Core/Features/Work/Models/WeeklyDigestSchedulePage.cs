using System.Globalization;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Private scheduling admission and its immutable original response; not a business aggregate.</summary>
public sealed record WeeklyDigestSchedulePage(Uuid TenantId, Uuid PageId, int Limit,
    ImmutableContentRef Plan, ImmutableContentRef? Result)
{
    public const string PlanSchema = "WeeklyDigestSchedulePage.Plan.v1";
    public const string ResultSchema = "WeeklyDigestSchedulePage.Result.v1";

    public static Uuid IdFor(Uuid tenantId, DateOnly week, string scheduleKey, string? cursor) =>
        Uuid.CreateVersion5(tenantId, "weekly-digest-page\n" +
            week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\n" + scheduleKey + "\n" +
            (cursor is null ? "first" : ContentDigest.Sha256(Encoding.UTF8.GetBytes(cursor))));
}
