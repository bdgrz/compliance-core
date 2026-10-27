using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
