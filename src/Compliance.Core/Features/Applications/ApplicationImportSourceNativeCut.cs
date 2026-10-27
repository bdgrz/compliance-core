using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Bounded proof of the actual hydrated native prefix, selected before an append.</summary>
sealed class ApplicationImportSourceNativeCut
{
    bool _verified;
    ulong _version;
    ulong _position;
    public string Digest { get; private set; } = ApplicationImportSourceDigest.Empty;
    public void Observe(DomainEvent ev)
    {
        Digest = ApplicationImportSourceDigest.Append(Digest, ev);
        _verified = false;
    }
    public void Confirm(Aggregate owner, string digest)
    {
        if (Digest != digest)
            throw new ImmutableContentIntegrityException("The application import source native history differs from the actual hydrated payload/metadata prefix.");
        (_verified, _version, _position) = (true, owner.Version, owner.CommittedStreamPosition);
    }
    public Result Require(Aggregate owner, NativeAcceptance acceptance, OperationProvenance operation)
    {
        if (owner.Version == 0 && owner.CommittedStreamPosition == 0 && Digest == ApplicationImportSourceDigest.Empty)
            return Result.Success;
        if (!_verified || _version != owner.Version || _position != owner.CommittedStreamPosition ||
            acceptance.SelectedLogicalId != operation.LogicalId || acceptance.SelectedFact is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An application import source append requires the exact selected finalized native owner cut.", true));
        return Result.Success;
    }
}
