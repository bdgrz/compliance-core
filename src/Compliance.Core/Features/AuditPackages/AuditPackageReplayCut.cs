using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Bounded proof of the actual hydrated native prefix, selected before an append.</summary>
sealed class AuditPackageReplayCut
{
    bool _verified;
    ulong _version;
    ulong _position;
    public string Digest { get; private set; } = AuditPackageReplayDigest.Empty;
    public void Observe(DomainEvent ev)
    {
        Digest = AuditPackageReplayDigest.Append(Digest, ev);
        _verified = false;
    }
    public void Confirm(Aggregate owner, string digest)
    {
        if (Digest != digest)
            throw new ImmutableContentIntegrityException("The audit-package native history differs from the actual hydrated payload/metadata prefix.");
        (_verified, _version, _position) = (true, owner.Version, owner.CommittedStreamPosition);
    }
    public Result Require(Aggregate owner, NativeAcceptance acceptance, OperationProvenance operation)
    {
        if (owner.Version == 0 && owner.CommittedStreamPosition == 0 && Digest == AuditPackageReplayDigest.Empty)
            return Result.Success;
        if (!_verified || _version != owner.Version || _position != owner.CommittedStreamPosition ||
            acceptance.SelectedLogicalId != operation.LogicalId || acceptance.SelectedFact is not null ||
            acceptance.SelectedCommandKind is { } kind && kind != operation.CommandKind)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An audit-package append requires the exact selected finalized native owner cut.", true));
        return Result.Success;
    }
}
