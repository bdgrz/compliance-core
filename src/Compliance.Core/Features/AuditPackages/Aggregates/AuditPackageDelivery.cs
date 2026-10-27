using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Aggregates;

/// <summary>One immutable personal handoff, materialized from its manifest admission on its own owner.</summary>
public sealed class AuditPackageDelivery : Aggregate
{
    public const string Area = "audit-package-deliveries";
    readonly Uuid _tenantId;
    readonly Uuid _packageId;
    readonly long _version;
    readonly Uuid _deliveryId;
    AuditPackageDelivered? _delivered;

    public AuditPackageDelivery(Uuid tenantId, Uuid packageId, long manifestVersion, Uuid deliveryId,
        Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, packageId, manifestVersion, deliveryId),
            new EventStreamAddress(tenantId.ToString(), Area,
                IdentityOf(tenantId, packageId, manifestVersion, deliveryId).ToString()))
    {
        (_tenantId, _packageId, _version, _deliveryId) = (tenantId, packageId, manifestVersion, deliveryId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AuditPackageDelivered>(fact =>
        {
            if (_delivered is not null || fact.TenantId != _tenantId || fact.SubjectId != Id ||
                fact.Revision != 1 || fact.OperationResult is null || fact.FirstResponse is not null ||
                fact.PackageId != _packageId || fact.PackageVersion != _version || fact.DeliveryId != _deliveryId ||
                fact.Operation.Owner != Stream || fact.Operation.RootOperationId != _deliveryId)
                throw new InvalidOperationException("A handoff is materialized once on its exact independent owner.");
            Acceptance.Observe(fact);
            _delivered = fact;
        });
    }

    public static Uuid IdentityOf(Uuid tenantId, Uuid packageId, long version, Uuid deliveryId) =>
        AggregateIdentity.Create(nameof(AuditPackageDelivery), tenantId, packageId,
            version.ToString(CultureInfo.InvariantCulture), deliveryId);
    public NativeAcceptance Acceptance { get; }
    public bool IsMaterialized => _delivered is not null;
    public AuditPackageDelivered? Delivered => _delivered;
    public long Revision => _delivered is null ? 0 : 1;

    public Result Materialize(Uuid programId, OperationProvenance operation, PackageDeliveryRecordingIntent intent,
        ImmutableContentRef frozen, NativeFactReference admission, ImmutableContentRef operationResult)
    {
        if (_delivered is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "This handoff already exists."));
        var manifest = new AuditPackageManifest(_tenantId, _packageId).Stream;
        if (programId == Uuid.Empty || intent.PackageId != _packageId || intent.PackageVersion != _version ||
            intent.DeliveryId != _deliveryId || intent.Header.RootOperationId != _deliveryId ||
            intent.Header.FirstOwner != manifest || intent.Header.AffectedOwner != manifest ||
            intent.Header.ExpectedRevisionOwner != manifest || admission.Stream != manifest ||
            admission.EventId == Uuid.Empty || admission.AggregateVersion == 0 ||
            operation.LogicalId == Uuid.Empty || operation.CommandKind != nameof(MaterializeRecordedAuditPackageDelivery) ||
            operation.Owner != Stream || operation.AffectedOwner != Stream || operation.ExpectedRevisionOwner != Stream ||
            operation.FirstOwner != manifest || operation.RootOperationId != _deliveryId ||
            operation.Episode != intent.Header.Episode || operation.OriginalExpectedRevision != intent.Header.OriginalExpectedRevision ||
            operation.AuthoredIntentSha256 != intent.Header.AuthoredIntentSha256 ||
            operation.CausationEventId != admission.EventId || operation.Actor != intent.Header.OriginalActor)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The handoff differs from its exact admitted historical attestation."));
        RaiseEvent(new AuditPackageDelivered(_tenantId, Id, 1, intent.RecorderMemberId, frozen,
            intent.RecordedAt, operation, null, operationResult, programId, _packageId, _version, _deliveryId,
            admission));
        return Result.Success;
    }
}
