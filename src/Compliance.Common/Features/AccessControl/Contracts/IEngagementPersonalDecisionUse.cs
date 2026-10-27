using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>A use receiver of the engagement personal decision process; it names the engagement that owns the decision.</summary>
public interface IEngagementPersonalDecisionUse
{
    /// <summary>The engagement that admitted the personal decision.</summary>
    Uuid EngagementId { get; }
}
