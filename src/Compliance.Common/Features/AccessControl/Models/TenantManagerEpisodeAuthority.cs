using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Bounded after-authority for the one affected membership episode; complete history remains external.</summary>
public sealed record TenantManagerEpisodeAuthority(Uuid EpisodeId, Uuid MemberId, bool HasContribution,
    bool IsRegistered, long AssignmentGeneration, IReadOnlyList<string> Reasons, bool Available,
    IReadOnlyList<string> NeutralReasons);
