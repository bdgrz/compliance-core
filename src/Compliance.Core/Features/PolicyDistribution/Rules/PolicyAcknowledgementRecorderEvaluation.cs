using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Rules;

/// <summary>A denial-only evaluation; ordinary membership, grants and campaign audience checks remain required.</summary>
public sealed record PolicyAcknowledgementRecorderEvaluation(PolicyAcknowledgementPersonSnapshot Person,
    bool IsSelf, bool CanRecordProxy);
