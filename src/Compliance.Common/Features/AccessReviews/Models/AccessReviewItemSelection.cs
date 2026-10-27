using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>One item a launch froze: its stable id and the reference to its frozen creation intent.</summary>
public sealed record AccessReviewItemSelection(Uuid ItemId, ImmutableContentRef Intent);
