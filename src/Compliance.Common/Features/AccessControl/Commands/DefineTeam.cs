using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Defines a team within a tenant. <paramref name="TeamId" /> is the immutable creation identity: an exact retry
///     replays the original acceptance and the same identity with a different name conflicts.
/// </summary>
[Discriminator("bdgrz.rbac.team.define", 1)]
public sealed record DefineTeam(Uuid TenantId, Uuid TeamId, string Name) : IRequest, IClientRbacMutationRequest, ICallable;
