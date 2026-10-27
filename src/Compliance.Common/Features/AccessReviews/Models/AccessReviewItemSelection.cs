using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>One item a launch froze: its stable id and the reference to its frozen creation intent.</summary>
public sealed record AccessReviewItemSelection(Uuid ItemId, ImmutableContentRef Intent);
