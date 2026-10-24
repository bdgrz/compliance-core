using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One item of one bulk decision batch admitted before a completion sealed the campaign. The completion intent
///     freezes the complete set; the completion holds an item only after every batch that selects it settled.
/// </summary>
public sealed record AccessReviewAdmittedBatchSelection(Uuid DecisionBatchId, Uuid ItemId, NativeFactReference Admission);
