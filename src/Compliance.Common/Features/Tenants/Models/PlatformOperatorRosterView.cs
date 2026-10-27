namespace Bdgrz.Compliance.Features.Tenants.Models;

public sealed record PlatformOperatorRosterView(ulong Revision,
    IReadOnlyList<PlatformOperatorAssignmentView> Operators);
