using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Policy and exact-source validation shared by the independent retention owners.</summary>
public static class ArtifactRetentionRules
{
    /// <summary>The existing seven-year retention policy; splitting owners does not authorize disposition.</summary>
    public const string Policy = "m0_d16_v1";

    public static bool IsSupportedSource(string kind) => kind is "evidence_artifact" or "application_import";
    public static bool ValidSource(ArtifactRetentionSource source) => source.TenantId != Uuid.Empty &&
        source.SourceId != Uuid.Empty && IsSupportedSource(source.SourceKind) &&
        source.ContentSha256 is { Length: 64 } &&
        source.ContentSha256.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool ValidActor(ActorReference actor) => actor is { Kind: "member", Display.Length: > 0 and <= 2000 } &&
        !string.IsNullOrWhiteSpace(actor.Display) && Uuid.TryParse(actor.Id, null, out var id) && id != Uuid.Empty;
    internal static bool ValidReason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Length <= 2000;
    internal static bool ValidPeriod(DateOnly first, DateOnly last) => first <= last && last.Year <= 9992;
    internal static string BasisKind(string kind) => kind == "evidence_artifact" ? "source_period" : "admin_period";
    internal static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));
    internal static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));

    internal static Result? Replay(NativeAcceptance acceptance, OperationProvenance operation) =>
        acceptance.Decide(AcceptedOperationBinding.Of(operation)) switch
        {
            NativeAcceptanceDecision.Replayed => Result.Success,
            NativeAcceptanceDecision.Conflict => Result.Failure(AcceptedAppendResult.ConflictError),
            _ => null,
        };

    internal static bool ValidOperation(OperationProvenance operation, EventStreamAddress owner,
        string kind, long expectedRevision) => operation.Owner == owner && operation.AffectedOwner == owner &&
        operation.ExpectedRevisionOwner == owner && operation.CommandKind == kind &&
        operation.LogicalId != Uuid.Empty && operation.RootOperationId == operation.LogicalId &&
        operation.OriginalExpectedRevision == (ulong)expectedRevision && ValidActor(operation.Actor) &&
        operation.AcceptedAt != default;
}
