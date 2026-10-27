using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Exact native operator intent/unknown-attempt authority and immutable original response binding.</summary>
public static class WorkDigestRetryContent
{
    public const string ChangeSchema = "work-digest-retry-authorization.v1";
    public const string ResultSchema = "work-digest-dispatch-status.v1";
    public const string MutationSchema = "mutation-result-content.v1";

    internal static void Validate(WorkDigestUnknownRetryAuthorized fact, WorkDigestDispatch owner,
        ulong expectedRevision, WorkDigestDispatchState? before = null)
    {
        if (fact.Operation is not { Actor: { Kind: "platform_operator" } actor } operation ||
            fact.Authored is null || fact.TenantId != owner.TenantId || fact.MemberId != owner.Id || fact.Revision != expectedRevision + 1 ||
            expectedRevision > long.MaxValue || fact.AuthorizedBy == Uuid.Empty ||
            actor.Id != fact.AuthorizedBy.ToString() || string.IsNullOrWhiteSpace(actor.Display) ||
            !AttributionMatches(fact.OriginalActor, fact.AuthorizedBy) ||
            operation.FirstOwner != owner.Stream || operation.AcceptedAt != fact.AuthorizedAt ||
            operation.AuthoredIntentSha256 is not { Length: 64 } ||
            !NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(AuthorizeWorkDigestUnknownRetry), expectedRevision) ||
            fact.PriorAttempt <= 0 || fact.MaximumAttempts <= fact.PriorAttempt ||
            !Reference(fact.Change, ChangeSchema) || !Reference(fact.ResponseContent, ResultSchema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, MutationSchema))
            throw Invalid("The retry authorization has another exact native owner/operator/revision or result contract.");
        var request = fact.Authored;
        if (request.CommandId != operation.LogicalId || request.TenantId != fact.TenantId || request.MemberId != fact.MemberId ||
            request.WeekOf != fact.WeekOf || request.ExpectedRevision < 0 || (ulong)request.ExpectedRevision != expectedRevision ||
            !Text(request.EvidenceReference, 256) || !Text(request.Rationale, 512) ||
            request.EvidenceReference.Trim() != fact.EvidenceReference || request.Rationale.Trim() != fact.Rationale ||
            operation.AuthoredIntentSha256 != AuthoredIntentDigest.Compute(request))
            throw Invalid("The authorization differs from its exact canonical authored request.");
        var response = Descriptor(fact.ResponseContent);
        if (!Matches(fact.FirstResponse!, response, FirstResponses.Schema) ||
            !Matches(fact.OperationResult!, new MutationResultContent(ResultSchema, response.Body), MutationSchema))
            throw Invalid("The authorization has another original first descriptor or mutation-result envelope.");
        if (before is null)
            return;
        var rules = new WorkDigestDispatchRules(owner.TenantId, owner.Id);
        rules.Restore(before);
        if (!rules.AuthorizeUnknownRetry(fact.WeekOf, fact.AuthorizedBy, fact.EvidenceReference, fact.Rationale,
                fact.AuthorizedAt, fact.MaximumAttempts) || before.MessageId != fact.MessageId ||
            before.Attempts != fact.PriorAttempt)
            throw Invalid("The authorization differs from its exact frozen unknown message/attempt/window.");
        var change = new WorkDigestRetryAuthorizationContent(request, actor, fact.OriginalActor,
            before.View(owner.TenantId, owner.Id) with { Revision = checked((long)expectedRevision) },
            before.RetryDeadline, fact.MaximumAttempts, fact.AuthorizedAt);
        if (!Matches(fact.Change, change, ChangeSchema) || !Matches(fact.ResponseContent, rules.Read(fact.WeekOf) with { Revision = checked((long)expectedRevision + 1) }, ResultSchema))
            throw Invalid("The authorization's immutable change/result differs from its exact prior authority.");
    }
    public static FirstResponseDescriptor Descriptor(ImmutableContentRef body) =>
        new(200, ImmutableContentStoreExtensions.JsonMediaType, ResultSchema, new StandardResponseBody(body));
    public static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null &&
        reference.ContentId != Uuid.Empty && reference.Schema == schema &&
        reference.MediaType == ImmutableContentStoreExtensions.JsonMediaType && reference.Bytes > 0 &&
        reference.Sha256 is { Length: 64 } && reference.Sha256.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool Matches<T>(ImmutableContentRef reference, T value, string schema) where T : notnull =>
        Reference(reference, schema) && reference.Sha256 == ContentDigest.OfJson(value) &&
        reference.Bytes == JsonSerializer.SerializeToUtf8Bytes(value,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!).Length;
    static bool AttributionMatches(ActorAttribution? attribution, Uuid userId) => attribution is not null &&
        !string.IsNullOrWhiteSpace(attribution.Issuer) && Uuid.TryParse(attribution.Subject,
            global::System.Globalization.CultureInfo.InvariantCulture, out var original) && original == userId;
    static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        !value.Any(char.IsControl);
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
