using Bdgrz.Compliance.Features.AccessControl.Commands;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The exact branch a guard-first administrator transition takes; it selects the one apply receiver.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AdministratorTransitionKind>))]
public enum AdministratorTransitionKind
{
    /// <summary>Withdraws availability before the member is suspended.</summary>
    [JsonStringEnumMemberName("suspend_member")]
    SuspendMember,

    /// <summary>Withdraws availability before the member episode is deprovisioned.</summary>
    [JsonStringEnumMemberName("deprovision_member")]
    DeprovisionMember,

    /// <summary>Withdraws availability before the administrators-team assignment is removed.</summary>
    [JsonStringEnumMemberName("remove_administrators_assignment")]
    RemoveAdministratorsAssignment,

    /// <summary>Reinstates a suspended member, then restores availability when nothing else withholds it.</summary>
    [JsonStringEnumMemberName("reinstate_member")]
    ReinstateMember,

    /// <summary>Re-assigns a removed administrators-team assignment, then restores availability.</summary>
    [JsonStringEnumMemberName("restore_administrators_assignment")]
    RestoreAdministratorsAssignment,

    /// <summary>A first administrators-team assignment of a member episode; availability registers after it.</summary>
    [JsonStringEnumMemberName("register_administrators_assignment")]
    RegisterAdministratorsAssignment,

    /// <summary>Suspend, deprovision or reinstate of a member episode that has no contribution and no pending transition.</summary>
    [JsonStringEnumMemberName("neutral_member_mutation")]
    NeutralMemberMutation,

    /// <summary>Administrators-team removal for an assignment that has no current contribution.</summary>
    [JsonStringEnumMemberName("neutral_assignment_mutation")]
    NeutralAssignmentMutation,
}
