using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.History;

/// <summary>Bounded proof of the actual hydrated native prefix, selected before an append.</summary>
sealed class AuditorRequestReplayCut
{
    bool _verified;
    ulong _version;
    ulong _position;
    public string Digest { get; private set; } = AuditorRequestReplayDigest.Empty;
    public void Observe(DomainEvent ev)
    {
        Digest = AuditorRequestReplayDigest.Append(Digest, ev);
        _verified = false;
    }
    public void Confirm(Aggregate owner, string digest)
    {
        if (Digest != digest)
            throw new ImmutableContentIntegrityException("The auditor request native history differs from the actual hydrated payload/metadata prefix.");
        (_verified, _version, _position) = (true, owner.Version, owner.CommittedStreamPosition);
    }
    public Result Require(Aggregate owner, NativeAcceptance acceptance, OperationProvenance operation)
    {
        if (owner.Version == 0 && owner.CommittedStreamPosition == 0 && Digest == AuditorRequestReplayDigest.Empty)
            return Result.Success;
        if (!_verified || _version != owner.Version || _position != owner.CommittedStreamPosition ||
            acceptance.SelectedLogicalId != operation.LogicalId || acceptance.SelectedFact is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An auditor request append requires the exact selected finalized native owner cut.", true));
        return Result.Success;
    }
}
