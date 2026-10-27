using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>A workforce-person owner and the actor snapshot that recorded the designation.</summary>
public sealed record ControlPersonOwnerDesignationView(Uuid PersonId, Uuid DesignationId,
    string Rationale, ActorReference Actor, DateTimeOffset DesignatedAt);
