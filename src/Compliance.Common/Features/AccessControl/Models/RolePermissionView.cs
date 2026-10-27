using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record RolePermissionView(Uuid RoleId, string Permission, Uuid AssignmentId, ulong Revision);
