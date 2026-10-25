using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The source terms selected inside one native routed-raise preparation.</summary>
sealed record RoutedFindingSource(FindingSource Source, string Title, string AffectedScope,
    Uuid OwnerMemberId, IReadOnlyList<FindingLink> Links, string? CorrectiveAction);
