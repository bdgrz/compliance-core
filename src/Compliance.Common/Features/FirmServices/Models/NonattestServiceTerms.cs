using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>The current corrected facts of one advisory service.</summary>
public sealed record NonattestServiceTerms(string ServiceKind, DateOnly StartedOn, DateOnly? EndedOn,
    Uuid? EngagementId = null);
