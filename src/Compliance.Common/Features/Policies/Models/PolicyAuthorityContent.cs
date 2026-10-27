using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyAuthorityContent(PolicyAuthorityHeader Header, IReadOnlyList<Uuid> PendingAuthorMemberIds,
    int CurrentApplicabilityCount, bool HasAccountableOwner);
