using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record NonattestServiceView(Uuid ClientTenantId, Uuid ServiceRecordId,
    NonattestServiceContent Content, ActorReference Actor, DateTimeOffset RecordedAt);
