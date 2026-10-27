using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

/// <summary>Reads one team by id.</summary>
[Discriminator("bdgrz.rbac.team.get", 1)]
public sealed record GetTeam(Uuid TenantId, Uuid TeamId) : IRequest<TeamView>, ITenantAccessRequest, ICallable;
