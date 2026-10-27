using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Independent review decision for one immutable template draft snapshot.</summary>
public sealed record MethodologyTemplateReviewView(
    Uuid ReviewId,
    string Outcome,
    string Comments,
    Uuid ReviewedByMemberId,
    string ReviewedByDisplay,
    DateTimeOffset ReviewedAt);
