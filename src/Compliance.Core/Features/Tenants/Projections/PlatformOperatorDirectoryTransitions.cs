using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Projections;

/// <summary>Applies source-ordered lifecycle facts to one operator row.</summary>
public static class PlatformOperatorDirectoryTransitions
{
    public static Uuid UserId(DomainEvent ev) => ev switch
    {
        PlatformOperatorAccessGranted e => e.UserId,
        PlatformOperatorBootstrapAccessGranted e => e.UserId,
        PlatformOperatorBootstrapGrantAndMarkAccepted e => e.UserId,
        PlatformOperatorAccessRevoked e => e.UserId,
        PlatformOperatorPrimaryMarkAccepted e => e.UserId,
        PlatformOperatorPrimaryUnmarked e => e.UserId,
        _ => throw Invalid()
    };

    public static ulong ExpectedRevision(DomainEvent ev) => ev switch
    {
        PlatformOperatorAccessGranted e => e.ExpectedRevision,
        PlatformOperatorBootstrapAccessGranted e => e.ExpectedRevision,
        PlatformOperatorBootstrapGrantAndMarkAccepted e => e.ExpectedRevision,
        PlatformOperatorAccessRevoked e => e.ExpectedRevision,
        PlatformOperatorPrimaryMarkAccepted e => e.ExpectedRevision,
        PlatformOperatorPrimaryUnmarked e => e.ExpectedRevision,
        _ => throw Invalid()
    };

    public static PlatformOperatorDirectoryRow? Apply(PlatformOperatorDirectoryRow? current, DomainEvent ev)
    {
        var userId = UserId(ev);
        if (userId == Uuid.Empty || (current is not null && current.UserId != userId))
            throw Invalid();
        return ev switch
        {
            PlatformOperatorAccessGranted e when current is null && e.OperationId != Uuid.Empty =>
                New(userId, e.OperationId),
            PlatformOperatorBootstrapAccessGranted e when current is null && e.OperationId != Uuid.Empty =>
                New(userId, e.OperationId),
            PlatformOperatorBootstrapGrantAndMarkAccepted e when current is null &&
                ValidMark(e.GrantEpisodeId, e.LineageId, e.Generation, e.ReservationEventId) =>
                new(userId, e.GrantEpisodeId, e.LineageId, e.Generation, e.ReservationEventId),
            PlatformOperatorAccessRevoked e when current is not null && current.GrantEpisodeId == e.GrantEpisodeId &&
                current.PrimaryGeneration == 0 => null,
            PlatformOperatorPrimaryMarkAccepted e when current is not null && current.GrantEpisodeId == e.GrantEpisodeId &&
                current.PrimaryGeneration == 0 && ValidMark(e.GrantEpisodeId, e.LineageId, e.Generation, e.ReservationEventId) =>
                current with
                {
                    PrimaryLineageId = e.LineageId,
                    PrimaryGeneration = e.Generation,
                    PrimaryReservationEventId = e.ReservationEventId
                },
            PlatformOperatorPrimaryUnmarked e when current is not null && current.GrantEpisodeId == e.GrantEpisodeId &&
                current.PrimaryLineageId == e.LineageId && current.PrimaryGeneration == e.Generation &&
                current.PrimaryReservationEventId == e.ReservationEventId && e.Generation > 0 && e.ReleaseEventId != Uuid.Empty =>
                current with
                {
                    PrimaryLineageId = Uuid.Empty,
                    PrimaryGeneration = 0,
                    PrimaryReservationEventId = Uuid.Empty
                },
            _ => throw Invalid()
        };
    }

    static bool ValidMark(Uuid grant, Uuid lineage, ulong generation, Uuid reservation) =>
        grant != Uuid.Empty && lineage != Uuid.Empty && generation > 0 && reservation != Uuid.Empty;

    static PlatformOperatorDirectoryRow New(Uuid userId, Uuid grant) =>
        new(userId, grant, Uuid.Empty, 0, Uuid.Empty);

    static InvalidOperationException Invalid() => new("The operator directory fact does not match its active grant and mark.");
}
