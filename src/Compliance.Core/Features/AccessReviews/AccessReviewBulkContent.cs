using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The authoritative permanent proof mismatch that cancels a bulk decision batch.</summary>
public sealed record AccessReviewBulkFailure(string Reason, Uuid? ItemId, NativeFactReference? Evidence);
