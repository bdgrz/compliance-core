using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

/// <summary>
///     Lists the roles granted to a team. <c>Limit</c> defaults to 50 and must be between 1 and
///     200; <c>Cursor</c> resumes a previous page; <c>Search</c> filters by a case-insensitive
///     role-ID substring; <c>Sort</c> is <c>"role_id"</c> or <c>"role_id:desc"</c> — roles have
///     exactly one index, so anything containing <c>"desc"</c> reverses it.
/// </summary>
[Discriminator("bdgrz.rbac.team-role.list", 1)]
public sealed record ListTeamRoles(
    Uuid TenantId,
    Uuid TeamId,
    int? Limit = null,
    string? Cursor = null,
    string? Search = null,
    string? Sort = null) : IRequest<Page<TeamRoleView>>, ITenantAccessRequest, ICallable;
