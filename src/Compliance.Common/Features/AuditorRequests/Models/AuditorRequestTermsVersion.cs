using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>A retained version of the auditor's requested wording and assignment terms.</summary>
public sealed record AuditorRequestTermsVersion(
    int Version,
    string Wording,
    string Scope,
    string Priority,
    Uuid OwnerMemberId,
    DateOnly DueOn,
    string ChangeReason,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt);
