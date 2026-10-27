using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work;

/// <summary>One exact finalized physical/native and selected-week cut, before any immutable candidate writes.</summary>
sealed record WorkDigestRetryPreparation(ulong Revision, ulong PhysicalPosition,
    WorkDigestDispatchStatusView Before, DateTimeOffset RetryDeadline, WorkDigestDispatchStatusView Result);
