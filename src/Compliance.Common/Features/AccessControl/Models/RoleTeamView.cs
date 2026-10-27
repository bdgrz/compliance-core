using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record RoleTeamView(Uuid RoleId, Uuid TeamId, Uuid AssignmentId, ulong Revision);
