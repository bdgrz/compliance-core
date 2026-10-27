using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record TeamRoleView(Uuid TeamId, Uuid RoleId, Uuid AssignmentId, ulong Revision);
