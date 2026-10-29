namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>Exactly one payload for the explicitly correlated canonical workforce record.</summary>
public sealed record WorkforceSourceFacts(WorkforcePersonSourceFacts? Person = null,
    WorkRelationshipTerms? WorkRelationship = null, WorkforceServiceSourceFacts? ServiceIdentity = null);
