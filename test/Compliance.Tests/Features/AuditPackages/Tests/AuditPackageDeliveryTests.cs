using Bdgrz.Compliance.Features.AuditPackages.Commands;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

public sealed class AuditPackageDeliveryTests
{
    static readonly DateTimeOffset RecordedAt = new(2026, 10, 17, 18, 4, 0, TimeSpan.Zero);
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid PackageId = Uuid.CreateVersion4();
    static readonly Uuid DeliveryId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid RecorderId = Uuid.CreateVersion4();
    static readonly ActorReference Recorder = ActorReference.ForMember(RecorderId, "Recorder");

    [Fact]
    public void ShouldDeriveStableIdentityGivenTenantPackageVersionAndDelivery()
    {
        // Arrange
        var first = new AuditPackageDelivery(TenantId, PackageId, 1, DeliveryId);
        var again = new AuditPackageDelivery(TenantId, PackageId, 1, DeliveryId, Uuid.CreateVersion4());

        // Act
        var otherVersion = AuditPackageDelivery.IdentityOf(TenantId, PackageId, 2, DeliveryId);
        var otherDelivery = AuditPackageDelivery.IdentityOf(TenantId, PackageId, 1, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Stream, again.Stream);
        Assert.NotEqual(first.Id, otherVersion);
        Assert.NotEqual(first.Id, otherDelivery);
        Assert.Equal(AuditPackageDelivery.Area, first.Stream.Area);
        Assert.False(first.IsMaterialized);
        Assert.Equal(0, first.Revision);
    }

    [Fact]
    public void ShouldMaterializeHandoffAtRevisionOneGivenExactAdmission()
    {
        // Arrange
        var owner = new AuditPackageDelivery(TenantId, PackageId, 3, DeliveryId);
        var input = Valid(owner, 3);

        // Act
        var result = Materialize(owner, input);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(owner.IsMaterialized);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, Pending(owner));
        var fact = owner.Delivered!;
        Assert.Equal(owner.Id, fact.SubjectId);
        Assert.Equal(3, fact.PackageVersion);
        Assert.Equal(DeliveryId, fact.DeliveryId);
        Assert.Equal(PackageId, fact.PackageId);
        Assert.Equal(ProgramId, fact.ProgramId);
        Assert.Equal(RecorderId, fact.ActorMemberId);
        Assert.Equal(RecordedAt, fact.OccurredAt);
        Assert.Equal(input.Admission, fact.Admission);
        Assert.Equal(input.Frozen, fact.Change);
        Assert.Null(fact.FirstResponse);
        Assert.Equal(TenantId, fact.TenantId);
        Assert.Equal(input.Operation, fact.Operation);
        Assert.Equal(input.OperationResult, fact.OperationResult);
    }

    [Theory]
    [InlineData("empty-program")]
    [InlineData("other-package")]
    [InlineData("other-version")]
    [InlineData("other-delivery")]
    [InlineData("other-root-operation")]
    [InlineData("other-first-owner")]
    [InlineData("other-admission-stream")]
    [InlineData("zero-admission-version")]
    [InlineData("empty-admission-event")]
    [InlineData("empty-logical-id")]
    [InlineData("other-command-kind")]
    [InlineData("other-operation-owner")]
    [InlineData("other-causation")]
    [InlineData("other-actor")]
    [InlineData("other-episode")]
    [InlineData("other-intent-hash")]
    [InlineData("other-header-affected-owner")]
    [InlineData("other-header-revision-owner")]
    [InlineData("other-operation-root")]
    [InlineData("other-operation-first-owner")]
    [InlineData("other-operation-affected-owner")]
    [InlineData("other-operation-revision-owner")]
    [InlineData("other-expected-revision")]
    public void ShouldRefuseAndAppendNothingGivenHandoffThatDiffersFromAdmission(string defect)
    {
        // Arrange
        var owner = new AuditPackageDelivery(TenantId, PackageId, 1, DeliveryId);
        var input = Defective(Valid(owner, 1), owner, defect);

        // Act
        var result = Materialize(owner, input);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.False(owner.IsMaterialized);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondMaterializationGivenHandoffAlreadyMaterialized()
    {
        // Arrange
        var owner = new AuditPackageDelivery(TenantId, PackageId, 1, DeliveryId);
        var input = Valid(owner, 1);
        Assert.True(Materialize(owner, input).IsSuccess);

        // Act
        var exact = Materialize(owner, input);
        var changed = Materialize(owner, input with { Frozen = NativeFacts.Ref("changed.v1") });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, exact.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRetainHandoffAndDecideReplayGivenReplayedHistory()
    {
        // Arrange
        var seed = new AuditPackageDelivery(TenantId, PackageId, 2, DeliveryId);
        var input = Valid(seed, 2);
        Assert.True(Materialize(seed, input).IsSuccess);
        var facts = NativeFacts.Raised(seed);

        // Act
        var owner = NativeFacts.Replay(new AuditPackageDelivery(TenantId, PackageId, 2, DeliveryId,
            input.Operation.LogicalId), facts);
        var revision = owner.Revision;
        var delivered = owner.Delivered;
        var exact = owner.Acceptance.Decide(AcceptedOperationBinding.Of(input.Operation));
        var changedBinding = AcceptedOperationBinding.Of(input.Operation with { AuthoredIntentSha256 = new string('d', 64) });
        var changed = owner.Acceptance.Decide(changedBinding);
        var retry = Materialize(owner, input);

        // Assert
        Assert.Equal(1, revision);
        Assert.Equal(input.Admission, delivered!.Admission);
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error!.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal((ulong)facts.Length, owner.Version);
    }

    [Fact]
    public void ShouldDecideNoneGivenUnrelatedLogicalIdOnReplayedHistory()
    {
        // Arrange
        var seed = new AuditPackageDelivery(TenantId, PackageId, 1, DeliveryId);
        var input = Valid(seed, 1);
        Assert.True(Materialize(seed, input).IsSuccess);
        var other = Uuid.CreateVersion4();

        // Act
        var owner = NativeFacts.Replay(new AuditPackageDelivery(TenantId, PackageId, 1, DeliveryId, other),
            NativeFacts.Raised(seed));
        var decision = owner.Acceptance.Decide(AcceptedOperationBinding.Of(input.Operation with { LogicalId = other }));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.None, decision);
        Assert.True(owner.IsMaterialized);
    }

    static int Pending(AuditPackageDelivery owner) =>
        new AggregateScenario<AuditPackageDelivery>(owner).PendingEvents.Count;

    static Result Materialize(AuditPackageDelivery owner, Input input) => owner.Materialize(input.ProgramId,
        input.Operation, input.Intent, input.Frozen, input.Admission, input.OperationResult);

    static Input Valid(AuditPackageDelivery owner, long version)
    {
        var manifest = new AuditPackageManifest(TenantId, PackageId).Stream;
        var header = new DurableIntentHeader(DeliveryId, manifest, manifest, manifest, Recorder, version.ToString(CultureInfo.InvariantCulture),
            3, new string('b', 64), RecordedAt);
        var content = NativeFacts.Ref("package-delivery-recording-intent.v1");
        var proofs = new ImmutableSelectionManifest(NativeFacts.Ref("proofs.v1"), "proof.v1", 1,
            SelectionOrder.Authored, new string('c', 64));
        var intent = new PackageDeliveryRecordingIntent(header, DeliveryId, PackageId, version, Uuid.CreateVersion4(),
            new string('e', 64), Uuid.CreateVersion4(), new string('f', 64), "auditor@example.test",
            "external-receipt", RecordedAt, RecordedAt, RecorderId,
            new NativeFactReference(manifest, Uuid.CreateVersion4(), 4), proofs);
        var admission = new NativeFactReference(manifest, Uuid.CreateVersion4(), 5);
        var operation = new OperationProvenance(Uuid.CreateVersion4(), DeliveryId, admission.EventId,
            nameof(MaterializeRecordedAuditPackageDelivery), Recorder, owner.Stream, header.Episode,
            header.OriginalExpectedRevision, header.AuthoredIntentSha256, RecordedAt, owner.Stream, owner.Stream,
            manifest);
        return new Input(ProgramId, operation, intent, content, admission, NativeFacts.Ref("unit-result.v1"));
    }

    static Input Defective(Input input, AuditPackageDelivery owner, string defect)
    {
        var foreign = new AuditPackageManifest(TenantId, Uuid.CreateVersion4()).Stream;
        var header = input.Intent.Header;
        return defect switch
        {
            "empty-program" => input with { ProgramId = Uuid.Empty },
            "other-package" => input with { Intent = input.Intent with { PackageId = Uuid.CreateVersion4() } },
            "other-version" => input with { Intent = input.Intent with { PackageVersion = 9 } },
            "other-delivery" => input with { Intent = input.Intent with { DeliveryId = Uuid.CreateVersion4() } },
            "other-root-operation" => input with
            {
                Intent = input.Intent with { Header = header with { RootOperationId = Uuid.CreateVersion4() } }
            },
            "other-first-owner" => input with
            {
                Intent = input.Intent with { Header = header with { FirstOwner = foreign } }
            },
            "other-admission-stream" => input with { Admission = input.Admission with { Stream = foreign } },
            "zero-admission-version" => input with { Admission = input.Admission with { AggregateVersion = 0 } },
            "empty-admission-event" => input with { Admission = input.Admission with { EventId = Uuid.Empty } },
            "empty-logical-id" => input with { Operation = input.Operation with { LogicalId = Uuid.Empty } },
            "other-command-kind" => input with { Operation = input.Operation with { CommandKind = "OtherCommand" } },
            "other-operation-owner" => input with { Operation = input.Operation with { Owner = foreign } },
            "other-causation" => input with { Operation = input.Operation with { CausationEventId = Uuid.CreateVersion4() } },
            "other-actor" => input with
            {
                Operation = input.Operation with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Other") }
            },
            "other-episode" => input with { Operation = input.Operation with { Episode = "99" } },
            "other-intent-hash" => input with { Operation = input.Operation with { AuthoredIntentSha256 = new string('9', 64) } },
            "other-header-affected-owner" => input with
            {
                Intent = input.Intent with { Header = header with { AffectedOwner = foreign } }
            },
            "other-header-revision-owner" => input with
            {
                Intent = input.Intent with { Header = header with { ExpectedRevisionOwner = foreign } }
            },
            "other-operation-root" => input with { Operation = input.Operation with { RootOperationId = Uuid.CreateVersion4() } },
            "other-operation-first-owner" => input with { Operation = input.Operation with { FirstOwner = foreign } },
            "other-operation-affected-owner" => input with { Operation = input.Operation with { AffectedOwner = foreign } },
            "other-operation-revision-owner" => input with
            {
                Operation = input.Operation with { ExpectedRevisionOwner = foreign }
            },
            "other-expected-revision" => input with { Operation = input.Operation with { OriginalExpectedRevision = 99 } },
            _ => throw new ArgumentOutOfRangeException(nameof(defect), defect, null),
        };
    }

    sealed record Input(Uuid ProgramId, OperationProvenance Operation, PackageDeliveryRecordingIntent Intent,
        ImmutableContentRef Frozen, NativeFactReference Admission, ImmutableContentRef OperationResult);
}
