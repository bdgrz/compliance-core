namespace Bdgrz.Compliance.Features.Tenants;

public sealed record PlatformOperatorRosterView(ulong Revision,
    IReadOnlyList<PlatformOperatorAssignmentView> Operators);
