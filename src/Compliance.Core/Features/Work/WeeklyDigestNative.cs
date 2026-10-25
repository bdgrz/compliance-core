using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>One exact identified receiver admission, including a revision-only receipt for a genuine no-change intent.</summary>
static class WeeklyDigestNative
{
    public const string ChangeSchema = "weekly-work-digest-receiver.v1";
    public const string MutationSchema = "mutation-result-content.v1";
    public static string Intent(WeeklyDigestMutationContent change) => change.Delivery is { } delivery
        ? AuthoredIntentDigest.Compute(delivery) : AuthoredIntentDigest.Compute(change.Recording!);
    public static Uuid CommandId(WeeklyDigestMutationContent change) => change.Delivery?.CommandId ?? change.Recording!.CommandId;
    public static long Expected(WeeklyDigestMutationContent change) => change.Delivery?.ExpectedRevision ?? change.Recording!.ExpectedRevision;
    public static Uuid SourceId(WeeklyDigestMutationContent change) => change.Delivery?.ScheduleEventId ?? change.Recording!.DeliveryRequestEventId;
    public static int Attempt(WeeklyDigestMutationContent change) => change.Delivery?.AttemptNumber ?? change.Recording!.AttemptNumber;
    public static Uuid Tenant(WeeklyDigestMutationContent change) => change.Delivery?.TenantId ?? change.Recording!.TenantId;
    public static Uuid Digest(WeeklyDigestMutationContent change) => change.Delivery?.DigestId ?? change.Recording!.DigestId;
    public static Result Evaluate(WeeklyWorkDigestRules rules, WeeklyDigestMutationContent change)
    {
        if (Expected(change) != rules.Revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, $"The weekly digest revision is {rules.Revision}."));
        if (change.Delivery is { } delivery)
            return change.SuppressionReason is { } reason
                ? rules.SuppressDelivery(delivery.ExpectedRevision, reason, change.Actor, change.AcceptedAt)
                : rules.RequestDelivery(delivery.ExpectedRevision, delivery.AttemptNumber, delivery.DeliveryKey,
                    change.Actor, change.AcceptedAt);
        var recording = change.Recording!;
        return rules.RecordDeliveryResult(recording.ExpectedRevision, recording.AttemptNumber, recording.DeliveryKey,
            recording.Outcome, recording.FailureCode, change.Actor, recording.AttemptedAt);
    }
    public static DomainEvent Fact(WeeklyDigestMutationContent change, OperationProvenance operation,
        ImmutableContentRef authored, ImmutableContentRef body, ImmutableContentRef first, ImmutableContentRef result)
    {
        var revision = change.Before.Revision + 1;
        if (change.Delivery is { } delivery)
        {
            if (change.SuppressionReason is { } reason)
                return new WeeklyWorkDigestDeliverySuppressed(delivery.TenantId, delivery.DigestId, change.Before.MemberId,
                    revision, reason, change.Actor, change.AcceptedAt)
                {
                    Operation = operation,
                    Authored = delivery,
                    OriginalActor = change.OriginalActor,
                    SourceProof = change.ScheduleSource!,
                    NoChange = change.NoChange,
                    Change = authored,
                    ResponseContent = body,
                    FirstResponse = first,
                    OperationResult = result
                };
            return new WeeklyWorkDigestDeliveryRequested(delivery.TenantId, delivery.DigestId, change.Before.MemberId,
                revision, delivery.AttemptNumber, delivery.DeliveryKey, change.Actor, change.AcceptedAt)
            {
                Operation = operation,
                Authored = delivery,
                OriginalActor = change.OriginalActor,
                SourceProof = change.ScheduleSource!,
                NoChange = change.NoChange,
                Change = authored,
                ResponseContent = body,
                FirstResponse = first,
                OperationResult = result
            };
        }
        var recording = change.Recording!;
        return new WeeklyWorkDigestDeliveryRecorded(recording.TenantId, recording.DigestId, change.Before.MemberId,
            revision, recording.AttemptNumber, recording.DeliveryKey, recording.Outcome, recording.FailureCode,
            change.Actor, recording.AttemptedAt)
        {
            Operation = operation,
            Authored = recording,
            OriginalActor = change.OriginalActor,
            SourceProof = change.RequestSource!,
            NoChange = change.NoChange,
            Change = authored,
            ResponseContent = body,
            FirstResponse = first,
            OperationResult = result
        };
    }
    public static WeeklyDigestMutationContent ChangeOf(DomainEvent value, WeeklyWorkDigestHeader before,
        WorkDigestDeliveryAttemptView? selected, WorkDigestDeliveryAttemptView? latest)
    {
        return value switch
        {
            WeeklyWorkDigestDeliveryRequested requested => new(nameof(DeliverWeeklyWorkDigest), requested.Authored, null,
                requested.Operation.Actor, requested.OriginalActor, requested.Operation.AcceptedAt, before, selected, latest,
                null, requested.SourceProof, null, requested.NoChange),
            WeeklyWorkDigestDeliverySuppressed suppressed => new(nameof(DeliverWeeklyWorkDigest), suppressed.Authored, null,
                suppressed.Operation.Actor, suppressed.OriginalActor, suppressed.Operation.AcceptedAt, before, selected, latest,
                suppressed.Reason, suppressed.SourceProof, null, suppressed.NoChange),
            WeeklyWorkDigestDeliveryRecorded recorded => new(nameof(RecordWorkDigestDelivery), null, recorded.Authored,
                recorded.Operation.Actor, recorded.OriginalActor, recorded.Operation.AcceptedAt, before, selected, latest,
                null, null, recorded.SourceProof, recorded.NoChange),
            _ => throw Invalid("The weekly native source has another fact kind.")
        };
    }
    public static void Structural(WeeklyWorkDigest owner, DomainEvent value, long beforeRevision)
    {
        if (value is not IWeeklyWorkDigestOperationFact fact || fact.Operation is not { Actor: { } actor } operation ||
            fact.Revision != beforeRevision + 1 || operation.LogicalId == Uuid.Empty || operation.RootOperationId != operation.LogicalId ||
            operation.Owner != owner.Stream || operation.FirstOwner != owner.Stream || operation.AffectedOwner != owner.Stream ||
            operation.ExpectedRevisionOwner != owner.Stream || operation.OriginalExpectedRevision != (ulong)beforeRevision ||
            operation.Episode != RootOperation.InitialEpisode || operation.AcceptedAt == default ||
            actor.Kind != "system_process" || actor.Id != WeeklyWorkDigest.DeliveryProcessId || string.IsNullOrWhiteSpace(actor.Display) ||
            operation.AuthoredIntentSha256 is not { Length: 64 } || fact.OriginalActor is not { Issuer: "bdgrz.system" } attribution ||
            attribution.Subject != actor.Id || !WorkDigestRetryContent.Reference(fact.Change, ChangeSchema) ||
            !WorkDigestRetryContent.Reference(fact.ResponseContent, UnitResult.Schema) ||
            !WorkDigestRetryContent.Reference(fact.FirstResponse, FirstResponses.Schema) ||
            !WorkDigestRetryContent.Reference(fact.OperationResult, MutationSchema))
            throw Invalid("The weekly receiver has another exact native owner, actor, revision or immutable response.");
        if (value is WeeklyWorkDigestDeliveryRequested { Authored: null } or WeeklyWorkDigestDeliverySuppressed { Authored: null } or
            WeeklyWorkDigestDeliveryRecorded { Authored: null })
            throw Invalid("The weekly receiver lacks its supplied command.");
        var header = owner.Header ?? throw Invalid("A weekly receiver cannot precede its schedule.");
        var change = ChangeOf(value, header, null, null);
        if (CommandId(change) != operation.LogicalId || Tenant(change) != owner.TenantId || Digest(change) != owner.Id ||
            Expected(change) != beforeRevision || change.CommandKind != operation.CommandKind ||
            operation.CausationEventId != SourceId(change) || Intent(change) != operation.AuthoredIntentSha256)
            throw Invalid("The weekly native fact differs from its supplied command and exact causal source.");
        var proof = (SourceFactProof?)change.ScheduleSource ?? change.RequestSource!;
        var kind = change.Delivery is not null ? EventKind.Of(typeof(WeeklyWorkDigestScheduled)) : EventKind.Of(typeof(WeeklyWorkDigestDeliveryRequested));
        if (proof is null || proof.Source != owner.Stream || proof.EventId != SourceId(change) || proof.OwnerRevision == 0 ||
            proof.OwnerRevision > (ulong)beforeRevision || proof.EventKind != kind || proof.PayloadSha256 is not { Length: 64 } ||
            !WorkDigestRetryContent.Reference(proof.RequiredFacts, SourceFactProofs.RequiredFactsSchema))
            throw Invalid("The weekly receiver has another source tuple, kind, or required-facts manifest.");
        var locator = new SourceFactLocator(proof.Source, proof.EventId, proof.PhysicalPosition, proof.RealmPosition,
            proof.GlobalPosition, proof.OwnerRevision, proof.PayloadSha256);
        if (!WorkDigestRetryContent.Matches(proof.RequiredFacts, locator, SourceFactProofs.RequiredFactsSchema))
            throw Invalid("The weekly source proof manifest differs from its complete exact source positions.");
        var descriptor = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(fact.ResponseContent));
        if (!WorkDigestRetryContent.Matches(fact.ResponseContent, new UnitResult(), UnitResult.Schema) ||
            !WorkDigestRetryContent.Matches(fact.FirstResponse!, descriptor, FirstResponses.Schema) ||
            !WorkDigestRetryContent.Matches(fact.OperationResult!, new MutationResultContent(UnitResult.Schema, descriptor.Body), MutationSchema))
            throw Invalid("The weekly receiver has another original Unit descriptor or mutation-result envelope.");
    }
    public static void VerifyPrepared(DomainEvent value, WeeklyDigestMutationContent change)
    {
        var native = (IWeeklyWorkDigestOperationFact)value;
        if (!WorkDigestRetryContent.Matches(native.Change, change, ChangeSchema) ||
            ContentDigest.Sha256(Fact(change, native.Operation, native.Change, native.ResponseContent, native.FirstResponse!, native.OperationResult!), value.GetType()) !=
            ContentDigest.Sha256(value, value.GetType()))
            throw Invalid("The weekly native fact differs from its exact prepared source/authority/mutation.");
    }
    public static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
