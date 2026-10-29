using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>The authoritative permanent proof mismatch that cancels a bulk decision batch.</summary>
public sealed record AccessReviewBulkFailure(string Reason, Uuid? ItemId, NativeFactReference? Evidence);
