using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Registered staff records ordered by staff member id; each record carries its own revision.</summary>
public sealed record FirmStaffDirectoryView(IReadOnlyList<FirmStaffMemberView> Staff);
