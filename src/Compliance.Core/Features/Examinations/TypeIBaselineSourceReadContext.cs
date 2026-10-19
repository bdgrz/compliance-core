using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Request authority and earlier source references available to one family reader.</summary>
public sealed record TypeIBaselineSourceReadContext(Uuid RequesterUserId,
    IReadOnlyList<TypeIBaselineSourceReference> PreviouslyCapturedSources);
