using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     One item of one bulk decision batch admitted before a completion sealed the campaign. The completion intent
///     freezes the complete set; the completion holds an item only after every batch that selects it settled.
/// </summary>
public sealed record AccessReviewAdmittedBatchSelection(Uuid DecisionBatchId, Uuid ItemId, NativeFactReference Admission);
