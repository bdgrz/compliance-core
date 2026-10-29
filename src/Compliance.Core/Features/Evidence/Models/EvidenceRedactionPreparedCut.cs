using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence.Models;

/// <summary>Exact native, physical, business and payload/metadata prefix prepared before append.</summary>
sealed record EvidenceRedactionPreparedCut(ulong Version, ulong PhysicalPosition, long Revision, string Digest, string MetadataDigest)
{
    public static EvidenceRedactionPreparedCut Of(EvidenceRedaction owner) =>
        new(owner.Version, owner.CommittedStreamPosition, owner.Revision, owner.ReplayCut.Digest, owner.ReplayCut.MetadataDigest);
}
