using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>The canonical complete current roster, read only through its native authority reference.</summary>
public sealed record PlatformOperatorAuthorityContent(Uuid RosterId, ulong Revision,
    PlatformOperatorAssignmentView[] Operators);
