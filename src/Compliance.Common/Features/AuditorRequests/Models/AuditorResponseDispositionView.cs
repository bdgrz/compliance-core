using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>An externally sourced disposition tied to the response version delivered.</summary>
public sealed record AuditorResponseDispositionView(
    int Version,
    string Outcome,
    string? ExternalReference,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset RecordedAt);
