using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public static class IndependenceCatalogContent
{
    public const string Realm = "bdgrz";
    public const string RuleSchema = "independence-rule-version-content.v1";
    public const string RatificationSchema = "independence-ratification-content.v1";
    public const string RuleResultSchema = "independence-rule-version-result.v1";
    public const string RatificationResultSchema = "independence-rule-ratification-result.v1";
    public static string Intent(IndependenceRuleVersionContent value) => AuthoredIntentDigest.Compute(new ReviseIndependenceRules(
        value.RequestId, value.Sequence - 1, value.Version.Content));
    public static string Intent(IndependenceRatificationContent value) => AuthoredIntentDigest.Compute(new RatifyIndependenceRuleVersion(
        value.RequestId, value.Ratification.RatificationId, value.Ratification.RuleVersion, value.Ratification.ObservedRuleCatalogSequence,
        value.Sequence - 1, value.Ratification.RuleContentDigest, value.Ratification.SourceReference, value.Sequence - 1));
    public static IndependenceRuleCatalogHeader Header(IndependenceRuleVersionContent value) => new(value.Sequence,
        value.Version.Version, IndependenceSourceDigest.RuleContent(value.Version.Content), value.Version.Actor, value.Version.RecordedAt);
    public static IndependenceRuleRatificationHeader Header(IndependenceRatificationContent value) => new(value.Sequence,
        value.Ratification.RatificationId, value.Ratification.RuleVersion, value.Ratification.RuleContentDigest,
        value.Ratification.Actor, value.Ratification.RecordedAt);
    public static bool ValidRule(IndependenceRuleVersionContent? value) => value is not null &&
        value.Owner == new IndependenceRuleCatalog().Stream && value.Sequence > 0 && value.Version is { } view && view.Version == value.Sequence &&
        !view.IsRatified && view.Ratification is null && IndependenceRecordValidation.ValidRules(view.Content) &&
        IndependenceRecordValidation.ValidAttribution(value.RequestId, view.Actor, view.RecordedAt, "platform_operator") &&
        (value.Sequence == 1 ? value.Previous is null : Reference(value.Previous, RuleSchema));
    public static bool ValidRatification(IndependenceRatificationContent? value) => value is not null &&
        value.Owner == new IndependenceRuleRatificationCatalog().Stream && value.Sequence > 0 && value.Ratification is { } item &&
        item.RatificationId != Uuid.Empty && item.RuleVersion > 0 && item.ObservedRuleCatalogSequence > 0 && Digest(item.RuleContentDigest) &&
        Bounded(item.SourceReference, 2000) && item.StaffMemberId != Uuid.Empty && item.UserId != Uuid.Empty && item.DirectoryStaffRevision > 0 &&
        item.DutyDesignationId != Uuid.Empty && item.DutyRevision > 0 &&
        IndependenceRecordValidation.ValidAttribution(value.RequestId, item.Actor, item.RecordedAt, "firm_staff") && item.Actor.Id == item.UserId.ToString() &&
        (value.Sequence == 1 ? value.Previous is null : Reference(value.Previous, RatificationSchema));
    public static bool Matches<T>(ImmutableContentRef? reference, T value, string schema) where T : notnull
    {
        if (!Reference(reference, schema))
            return false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeof(T), ComplianceCoreJsonContext.Default);
        return reference!.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }
    public static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.ContentId != Uuid.Empty &&
        value.Bytes >= 0 && value.MediaType == ImmutableContentStoreExtensions.JsonMediaType && value.Schema == schema && Digest(value.Sha256);
    public static bool Digest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    static bool Bounded(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        !value.Any(char.IsControl) && value == value.Trim();
    public static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeResultAsync<T>(IImmutableContentStore content,
        string schema, T value, CancellationToken ct = default) where T : notnull
    {
        var body = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), schema, value, ct).ConfigureAwait(false);
        var result = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(schema, new StandardResponseBody(body)), ct).ConfigureAwait(false);
        return (new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(body)), result);
    }
    public static async ValueTask<Result<T>> ResponseAsync<T>(IImmutableContentStore content, Result<AcceptedAppendResult> result,
        string schema, CancellationToken ct = default) where T : notnull
    {
        if (!result.IsSuccess)
            return Result<T>.Failure(result.Error!);
        var response = result.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (response is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (response.HttpStatus != 200 || response.ResultSchema != schema || response.Body is not StandardResponseBody body || body.Content.Schema != schema)
            throw new ImmutableContentIntegrityException("The original catalog response has another exact status, schema or body.");
        return Result<T>.Success(await content.ReadJsonAsync<T>(Realm, body.Content, ct).ConfigureAwait(false));
    }
    public static RequestError Failure(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The exact immutable independence catalog authority cannot be verified.", error is ImmutableContentUnavailableException);
}
