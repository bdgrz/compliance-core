using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Bounded proof of the actual hydrated native prefix, selected before an append.</summary>
sealed class ProviderGapReplayCut
{
    bool _verified;
    ulong _version;
    ulong _position;
    public string Digest { get; private set; } = ProviderGapReplayDigest.Empty;
    public void Observe(DomainEvent ev)
    {
        Digest = ProviderGapReplayDigest.Append(Digest, ev);
        _verified = false;
    }
    public void Confirm(Aggregate owner, string digest)
    {
        if (Digest != digest)
            throw new ImmutableContentIntegrityException("The provider gap native history differs from the actual hydrated payload/metadata prefix.");
        (_verified, _version, _position) = (true, owner.Version, owner.CommittedStreamPosition);
    }
    public Result Require(Aggregate owner, NativeAcceptance acceptance, OperationProvenance operation)
    {
        if (owner.Version == 0 && owner.CommittedStreamPosition == 0 && Digest == ProviderGapReplayDigest.Empty)
            return Result.Success;
        if (!_verified || _version != owner.Version || _position != owner.CommittedStreamPosition ||
            acceptance.SelectedLogicalId != operation.LogicalId || acceptance.SelectedFact is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A provider gap append requires the exact selected finalized native owner cut.", true));
        return Result.Success;
    }
}
