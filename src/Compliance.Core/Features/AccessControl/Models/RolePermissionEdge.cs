using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

sealed record RolePermissionEdge(Uuid RoleId, string Permission);
