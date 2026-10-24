using Bdgrz.Compliance.Features.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Builds and compares authored bindings while retained owners migrate their business facts.</summary>
public static class AuthoredCommandReplay
{
    public static OperationProvenance Create(Aggregate owner, Uuid commandId, string kind,
        Uuid actorId, long? expectedRevision, object authoredIntent, DateTimeOffset acceptedAt,
        string actorKind = "member")
    {
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(authoredIntent)))
            .ToLowerInvariant();
        // Presentation names and candidate clocks are not authored operation identity.
        var actor = new ActorReference(actorKind, actorId.ToString(), actorId.ToString());
        return new OperationProvenance(commandId, commandId, null, kind, actor, owner.Stream,
            owner.Id.ToString(), expectedRevision is { } revision ? unchecked((ulong)revision) : null,
            digest, acceptedAt, owner.Stream, owner.Stream);
    }

    public static bool TryReplay(NativeAcceptance acceptance, OperationProvenance? operation,
        out CommandFailure? failure)
    {
        failure = null;
        if (operation is null)
            return false;
        if (operation.LogicalId == Uuid.Empty)
        {
            failure = CommandFailure.InvalidContent("A command requires its stable nonempty identity.");
            return true;
        }
        // Creation reserves its immutable resource ID, including retained creation facts without provenance.
        if (operation.LogicalId.ToString() == operation.Episode)
        {
            failure = CommandFailure.StateConflict("A mutation cannot reuse its resource's creation identity.");
            return true;
        }
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(operation));
        if (decision == NativeAcceptanceDecision.None)
            return false;
        if (decision == NativeAcceptanceDecision.Conflict)
            failure = CommandFailure.StateConflict("The command identity already belongs to different authored intent.");
        return true;
    }

    public static Result? Unit(NativeAcceptance acceptance, OperationProvenance operation) =>
        TryReplay(acceptance, operation, out var failure)
            ? failure is null ? Result.Success : Result.Failure(failure.ToRequestError())
            : null;

    public static void Observe(Aggregate owner, NativeAcceptance acceptance, DomainEvent fact, OperationProvenance? operation)
    {
        if (operation is not null)
        {
            OwnerProvenance.RequireOwn(owner, operation.LogicalId, operation);
            acceptance.Observe(fact, operation);
        }
    }
}
