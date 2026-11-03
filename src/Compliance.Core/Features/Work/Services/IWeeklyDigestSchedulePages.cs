using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Models;

namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Durable, insert-once technical page admission and completion.</summary>
public interface IWeeklyDigestSchedulePages
{
    Task<WeeklyDigestSchedulePage?> ReadAsync(Uuid tenantId, Uuid pageId, CancellationToken ct);
    Task<Result<WeeklyDigestSchedulePage>> AcceptIfAbsentAsync(WeeklyDigestSchedulePage candidate,
        CancellationToken ct);
    Task<WeeklyDigestSchedulePage> CompleteAsync(WeeklyDigestSchedulePage admitted,
        ImmutableContentRef result, CancellationToken ct);
}
