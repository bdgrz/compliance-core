using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>Active incident gates and lightweight immutable assessment commitments; historical bodies are not retained here.</summary>
public sealed record ComplianceIncidentCurrentAuthority(ImmutableContentRef VersionContent, ImmutableContentRef? AssessmentContent,
    ImmutableContentRef? DecisionContent, ImmutableContentRef? ResolutionContent, IReadOnlyList<ComplianceIncidentAssessmentToken> Assessments);
