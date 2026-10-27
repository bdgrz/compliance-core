using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Aggregates;

/// <summary>Compact authority on one stream preserves the global nonempty-roster invariant.</summary>
public sealed class PlatformOperatorRoster : Aggregate
{
    static readonly Uuid RosterId = Uuid.Parse(
        "bc279ad5-e9ae-5033-a174-4d1350ce2b07", CultureInfo.InvariantCulture);

    public PlatformOperatorRoster(Uuid selectedCommandId = default)
        : base(RosterId, new EventStreamAddress("bdgrz", "platform-operator-roster", RosterId.ToString()))
    {
        Acceptance = new(selectedCommandId);
        On<PlatformOperatorRosterSeeded>(Apply);
        On<PlatformOperatorGranted>(Apply);
        On<PlatformOperatorRevoked>(Apply);
    }

    public static Uuid CreationId => RosterId;
    public NativeAcceptance Acceptance { get; }
    public bool IsInitialized => CurrentAuthority is not null;
    public ulong Revision { get; private set; }
    public int ActiveCount { get; private set; }
    public ImmutableContentRef? CurrentAuthority { get; private set; }

    public Result Append(DomainEvent value, ulong preparedRevision, ulong preparedPhysicalPosition,
        PlatformOperatorMutationContent mutation, PlatformOperatorAuthorityContent before,
        PlatformOperatorAuthorityContent after, FirstResponseDescriptor response)
    {
        if (preparedRevision != Revision || preparedPhysicalPosition != CommittedStreamPosition)
            return Failure(RequestErrorKind.Conflict, "The roster changed while its exact authority was prepared.");
        try
        {
            var fact = value as IPlatformOperatorFact ?? throw new InvalidOperationException();
            Validate(value, fact);
            PlatformOperatorRosterContent.RequireAuthority(before, Revision, ActiveCount);
            Require(CurrentAuthority is null ? Revision == 0 && before.Operators.Length == 0 :
                PlatformOperatorRosterContent.Matches(CurrentAuthority, before, PlatformOperatorRosterContent.AuthoritySchema));
            var evaluated = PlatformOperatorRosterRules.Apply(before, mutation);
            if (!evaluated.IsSuccess)
                return Result.Failure(evaluated.Error!);
            Require(ContentDigest.OfJson(evaluated.Value!) == ContentDigest.OfJson(after) &&
                PlatformOperatorRosterContent.Matches(fact.Authority, after, PlatformOperatorRosterContent.AuthoritySchema) &&
                PlatformOperatorRosterContent.Matches(fact.Change, mutation, PlatformOperatorRosterContent.ChangeSchema));
            var expected = PlatformOperatorRosterContent.Fact(mutation, fact.Operation, fact.Change,
                fact.Authority, fact.FirstResponse!, fact.OperationResult!, after);
            Require(ContentDigest.Sha256(expected, expected.GetType()) == ContentDigest.Sha256(value, value.GetType()));
            Require(fact.Operation.AuthoredIntentSha256 == PlatformOperatorRosterContent.IntentDigest(mutation));
            Require(response.HttpStatus == 200 && response.ContentType == "application/json" && response.ResultSchema == UnitResult.Schema &&
                response.Body is StandardResponseBody body && body.Content == fact.OperationResult &&
                PlatformOperatorRosterContent.Matches(body.Content, new UnitResult(), UnitResult.Schema) &&
                PlatformOperatorRosterContent.Matches(fact.FirstResponse!, response, FirstResponses.Schema));
            Require(DomainEventPayload.Size(value) <= DomainEventPayload.MaximumBytes);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ImmutableContentIntegrityException)
        {
            return Failure(RequestErrorKind.Validation, "The roster fact differs from its exact prepared native authority.");
        }
        RaiseEvent(value);
        return Result.Success;
    }

    void Apply(PlatformOperatorRosterSeeded value) => ApplyFact(value);
    void Apply(PlatformOperatorGranted value) => ApplyFact(value);
    void Apply(PlatformOperatorRevoked value) => ApplyFact(value);

    void ApplyFact(DomainEvent value)
    {
        var fact = (IPlatformOperatorFact)value;
        Validate(value, fact);
        Acceptance.Observe(value);
        CurrentAuthority = fact.Authority;
        Revision = fact.Revision;
        ActiveCount = fact.ActiveCount;
    }

    void Validate(DomainEvent value, IPlatformOperatorFact fact)
    {
        // Retained legacy facts have no trusted immutable authority or original native response. They must not
        // silently become qualified native acceptances; an explicit trusted conversion is a separate protocol.
        if (fact.Operation is not { Actor: not null } operation)
            throw new InvalidOperationException("A native roster fact requires its original operation and actor.");
        OwnerProvenance.RequireOwn(this, operation.LogicalId, operation);
        Require(operation.RootOperationId == operation.LogicalId && operation.CausationEventId is null &&
            operation.AffectedOwner == Stream && operation.ExpectedRevisionOwner == Stream &&
            (operation.FirstOwner is null || operation.FirstOwner == Stream) && operation.Episode == Id.ToString() &&
            operation.AcceptedAt != default && operation.AuthoredIntentSha256 is { Length: 64 } &&
            operation.AuthoredIntentSha256.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            fact.Revision == Revision + 1 && fact.ActiveCount > 0 &&
            PlatformOperatorRosterContent.Reference(fact.Authority, PlatformOperatorRosterContent.AuthoritySchema) &&
            PlatformOperatorRosterContent.Reference(fact.Change, PlatformOperatorRosterContent.ChangeSchema) &&
            PlatformOperatorRosterContent.Reference(fact.FirstResponse, FirstResponses.Schema) &&
            PlatformOperatorRosterContent.Reference(fact.OperationResult, UnitResult.Schema));
        switch (value)
        {
            case PlatformOperatorRosterSeeded seed:
                Require(!IsInitialized && Revision == 0 && seed.RosterId == Id && seed.UserIds.Length == 0 &&
                    operation.LogicalId == Id && operation.CommandKind == nameof(SeedPlatformOperatorRoster) &&
                    operation.OriginalExpectedRevision is null && operation.Actor.Kind == "system_process" &&
                    operation.Actor.Id == "platform-operator-bootstrap" && RequestActorAttribution(seed.Actor) &&
                    seed.OccurredAt == operation.AcceptedAt);
                break;
            case PlatformOperatorGranted grant:
                Require(IsInitialized && operation.CommandKind == nameof(GrantPlatformOperator) &&
                    grant.CommandId == operation.LogicalId && grant.ExpectedRevision == Revision &&
                    operation.OriginalExpectedRevision == Revision && fact.ActiveCount == ActiveCount + 1 &&
                    ValidChange(grant.ActorUserId, grant.SubjectUserId, grant.Reason, grant.Actor, operation) &&
                    grant.ActorDisplay == operation.Actor.Display && grant.OccurredAt == operation.AcceptedAt);
                break;
            case PlatformOperatorRevoked revoke:
                Require(IsInitialized && operation.CommandKind == nameof(RevokePlatformOperator) &&
                    revoke.CommandId == operation.LogicalId && revoke.ExpectedRevision == Revision &&
                    operation.OriginalExpectedRevision == Revision && fact.ActiveCount == ActiveCount - 1 &&
                    revoke.AssignmentId != Uuid.Empty &&
                    ValidChange(revoke.ActorUserId, revoke.SubjectUserId, revoke.Reason, revoke.Actor, operation) &&
                    revoke.ActorDisplay == operation.Actor.Display && revoke.OccurredAt == operation.AcceptedAt);
                break;
            default:
                throw new InvalidOperationException("Unknown roster fact.");
        }
    }

    static bool ValidChange(Uuid actor, Uuid subject, string reason, ActorAttribution attribution,
        OperationProvenance operation) => actor != Uuid.Empty && subject != Uuid.Empty &&
        PlatformOperatorRosterRules.ValidReason(reason) && reason == reason.Trim() &&
        RequestActorAttribution(attribution) && attribution.Subject == actor.ToString() &&
        operation.Actor.Kind == "platform_operator" && operation.Actor.Id == actor.ToString();
    static bool RequestActorAttribution(ActorAttribution actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Subject) && !string.IsNullOrWhiteSpace(actor.Issuer);
    static void Require(bool valid)
    {
        if (!valid)
            throw new InvalidOperationException("The roster requires its exact original native authority and operation.");
    }
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
