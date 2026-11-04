using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>Current lifecycle state; prior versions remain in the record stream.</summary>
public sealed record NonattestServiceRecordView(Uuid TenantId, Uuid RecordId, long Revision,
    NonattestServiceTerms Terms, bool IsVoided, string? Reason, Uuid ActorUserId, DateTimeOffset RecordedAt);
