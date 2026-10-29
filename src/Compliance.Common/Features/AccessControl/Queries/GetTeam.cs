using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

/// <summary>Reads one team by id.</summary>
[Discriminator("bdgrz.rbac.team.get", 1)]
public sealed record GetTeam(Uuid TenantId, Uuid TeamId) : IRequest<TeamView>, ITenantAccessRequest, ICallable;
