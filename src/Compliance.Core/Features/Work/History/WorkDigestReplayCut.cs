using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Work.History;

/// <summary>Bounded complete replay commitment and exact finalized native/physical append cut.</summary>
sealed class WorkDigestReplayCut
{
    bool _verified;
    ulong _version;
    ulong _position;
    public string Digest { get; private set; } = WorkDigestReplayDigest.Empty;
    public void Observe(DomainEvent fact)
    {
        Digest = WorkDigestReplayDigest.Append(Digest, fact);
        _verified = false;
    }
    public void Confirm(Aggregate owner, string digest)
    {
        if (Digest != digest)
            throw new ImmutableContentIntegrityException("The digest raw source differs from its actual hydrated payload/metadata prefix.");
        (_verified, _version, _position) = (true, owner.Version, owner.CommittedStreamPosition);
    }
    public bool Allows(Aggregate owner) => owner.Version == 0 && owner.CommittedStreamPosition == 0 &&
        Digest == WorkDigestReplayDigest.Empty || _verified && _version == owner.Version && _position == owner.CommittedStreamPosition;
}
