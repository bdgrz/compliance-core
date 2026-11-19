using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>Current individual grant and bounded primary-seat state read from their source owners.</summary>
/// <remarks>The two owners are read separately. Each version is a conditional-write token for its own owner.</remarks>
public sealed record PlatformOperatorStateView(Uuid UserId, bool IsActive, Uuid GrantEpisodeId,
    ulong OperatorRevision, ulong MarkGeneration, ulong PrimaryRevision, bool IsBootstrapComplete,
    int OccupiedPrimarySeats, int ConfirmedPrimarySeats, ulong SeatGeneration, string? SeatStatus);
