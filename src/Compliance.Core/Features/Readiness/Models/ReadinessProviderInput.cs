using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>Provider facts, reviews and source gaps available to one as-of readiness run.</summary>
public sealed record ReadinessProviderInput(ProviderView Provider,
    IReadOnlyList<AssuranceReportView> Reports, IReadOnlyList<ProviderReviewView> Reviews,
    IReadOnlyList<ProviderCoverageGapView>? CoverageGaps = null);
