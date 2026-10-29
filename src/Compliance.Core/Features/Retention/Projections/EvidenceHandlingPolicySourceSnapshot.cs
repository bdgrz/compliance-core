using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Projections;

/// <summary>The current inspected artifact metadata used to fence a policy assignment.</summary>
public sealed record EvidenceHandlingPolicySourceSnapshot(
    ArtifactRetentionSource Source,
    ulong Position,
    long ContentLength,
    string SourceState,
    string SourceHandlingClass);
