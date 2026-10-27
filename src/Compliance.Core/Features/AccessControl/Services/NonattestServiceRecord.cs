using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>A recorded period of nonattest service provided by the firm to one client.</summary>
public sealed class NonattestServiceRecord
{
    public NonattestServiceRecord(Uuid clientTenantId, Uuid serviceEngagementId, string serviceType,
        DateOnly startedOn, DateOnly? endedOn, IReadOnlyList<Uuid> firmStaffMemberIds,
        bool involvedManagementFunctions)
    {
        ArgumentNullException.ThrowIfNull(firmStaffMemberIds);
        ClientTenantId = clientTenantId;
        ServiceEngagementId = serviceEngagementId;
        ServiceType = serviceType;
        StartedOn = startedOn;
        EndedOn = endedOn;
        FirmStaffMemberIds = Array.AsReadOnly(firmStaffMemberIds.ToArray());
        InvolvedManagementFunctions = involvedManagementFunctions;
    }

    public Uuid ClientTenantId { get; }
    public Uuid ServiceEngagementId { get; }
    public string ServiceType { get; }
    public DateOnly StartedOn { get; }
    public DateOnly? EndedOn { get; }
    public IReadOnlyList<Uuid> FirmStaffMemberIds { get; }
    public bool InvolvedManagementFunctions { get; }
}
