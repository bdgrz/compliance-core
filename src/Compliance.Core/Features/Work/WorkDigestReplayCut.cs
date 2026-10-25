using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

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
