using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The replayed audience membership of one person in a campaign: how and when the person was included, and the
///     campaign fact that last included the person. The results of the person live on the participant owner.
/// </summary>
sealed class CampaignParticipant(Uuid personId, string displayName, string workerType,
    string? department, bool launchMember, string inclusion, Uuid includedBy, DateOnly dueOn,
    NativeFactReference includingFact)
{
    public Uuid PersonId { get; } = personId;
    public string DisplayName { get; set; } = displayName;
    public string WorkerType { get; set; } = workerType;
    public string? Department { get; set; } = department;
    public bool LaunchMember { get; } = launchMember;
    public string Inclusion { get; set; } = inclusion;
    public Uuid IncludedBy { get; set; } = includedBy;
    public DateOnly DueOn { get; set; } = dueOn;
    public string? RemovedReason { get; set; }
    public Uuid? RemovedBy { get; set; }
    public NativeFactReference IncludingFact { get; set; } = includingFact;
    public bool IsActive => RemovedReason is null;
}
