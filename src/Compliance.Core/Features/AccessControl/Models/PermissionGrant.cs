using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

sealed record PermissionGrant(Uuid MemberId, string Permission);
