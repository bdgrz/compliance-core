using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Actual predecessor locator and complete captured target commitments; manual row fields remain declarations.</summary>
public sealed record AuditPopulationDecisionSource(SourceFactLocator Predecessor, string PredecessorMetadataSha256, SourceFactLocator CloseFact, string CloseMetadataSha256,
    long Revision, ulong PhysicalPosition, string PayloadPrefixSha256, string MetadataPrefixSha256,
    string PredecessorContentSha256, Uuid CloseSnapshotId, string CloseSnapshotSha256)
{
    public const string Schema = "audit-population-decision-source.v1";
}
