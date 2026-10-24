using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A use receiver of the engagement personal decision process; it names the engagement that owns the decision.</summary>
public interface IEngagementPersonalDecisionUse
{
    /// <summary>The engagement that admitted the personal decision.</summary>
    Uuid EngagementId { get; }
}
