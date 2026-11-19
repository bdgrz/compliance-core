using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>Authored identity and attribution retained with a business fact.</summary>
public sealed record FirmServiceReceipt(Uuid CommandId, long ExpectedRevision, string AuthoredIntentSha256,
    Uuid ActorUserId, DateTimeOffset OccurredAt);
