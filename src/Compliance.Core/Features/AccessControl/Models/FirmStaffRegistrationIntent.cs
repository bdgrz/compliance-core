using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     The frozen terms of one staff registration: normalized source values and the authenticated operator. Claim and
///     completion copy it; status changes cannot replace the user, practice or source.
/// </summary>
public sealed record FirmStaffRegistrationIntent(DurableIntentHeader Header, Uuid StaffMemberId,
    Uuid CanonicalUserId, string Practice, string SourceReference, string InitialStatus)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "firm-staff-registration-intent.v1";

    /// <summary>The only initial status; a registered staff member starts active.</summary>
    public const string ActiveStatus = "active";
}
