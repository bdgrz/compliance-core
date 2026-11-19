using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Projections;

/// <summary>One active grant and its exact current primary mark, if any.</summary>
public sealed record PlatformOperatorDirectoryRow(Uuid UserId, Uuid GrantEpisodeId,
    Uuid PrimaryLineageId, ulong PrimaryGeneration, Uuid PrimaryReservationEventId);
