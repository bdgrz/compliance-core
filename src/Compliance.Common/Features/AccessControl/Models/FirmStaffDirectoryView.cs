using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Registered staff records ordered by staff member id; each record carries its own revision.</summary>
public sealed record FirmStaffDirectoryView(IReadOnlyList<FirmStaffMemberView> Staff);
