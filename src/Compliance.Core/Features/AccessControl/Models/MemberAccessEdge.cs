using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record MemberAccessEdge(Uuid TeamId, Uuid RoleId,
    IReadOnlyList<string> Permissions);
