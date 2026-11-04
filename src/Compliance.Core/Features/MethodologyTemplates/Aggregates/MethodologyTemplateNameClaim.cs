using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>The authoritative holder of one normalized platform-global methodology name.</summary>
public sealed class MethodologyTemplateNameClaim : NameClaim
{
    public const string Area = "methodology-template-names";
    public const string CreationSchema = "methodology-template-creation-intent.v1";
    public const string ConflictSchema = "methodology-template-creation-conflict.v1";
    public const string CreationCommandKind = "CreateMethodologyTemplateV3";
    public const string RefusalCommandKind = "RefuseMethodologyTemplateCreation";
    public const string ProcessId = "methodology-template-creation";
    const string RefusalReason = "The template identity is already bound to another creation.";

    public MethodologyTemplateNameClaim(string name, Uuid selectedLogicalId = default)
        : base(Identity(name), AddressOf(name), selectedLogicalId)
    {
        NormalizedName = NameNormalization.Normalize(name);
        On<MethodologyTemplateNameClaimed>(Apply);
        On<MethodologyTemplateCreationRefused>(Apply);
    }

    public string NormalizedName { get; }

    public static Uuid Identity(string name)
    {
        if (!Text(name, 200))
            throw new ArgumentException("A methodology name requires bounded original firm text.", nameof(name));
        return AggregateIdentity.Create(nameof(MethodologyTemplateNameClaim), MethodologyTemplate.Realm,
            NameNormalization.Normalize(name));
    }
    public static EventStreamAddress AddressOf(string name) => new(MethodologyTemplate.Realm, Area, Identity(name).ToString());

    public Result PrepareCreation(OperationProvenance operation, MethodologyTemplateCreationIntent intent, ImmutableContentRef content)
    {
        if (!ValidIntent(intent) || NameNormalization.Normalize(intent.Name) != NormalizedName ||
            !Manifest(intent, content, CreationSchema) || operation.LogicalId != intent.CommandId ||
            operation.Actor.Id != intent.CreatedBy.ToString())
            return Invalid("A name admission must preserve the exact original author, name and target in its frozen content.");
        var holder = new MethodologyTemplate(intent.TemplateId).Stream;
        if (!CreationBinding(operation, holder, content))
            return Invalid("A name admission requires its exact platform operator, line and frozen creation intent.");
        if (Replay(operation) is { } replay)
            return replay;
        if (Version >= long.MaxValue)
            return Conflict("The name admission generation cannot advance further.");
        return CheckClaim(operation, holder, (long)Version + 1);
    }

    public Result ClaimCreation(OperationProvenance operation, MethodologyTemplateCreationIntent intent, ImmutableContentRef content,
        ImmutableContentRef firstResponse)
    {
        var prepared = PrepareCreation(operation, intent, content);
        if (!prepared.IsSuccess)
            return prepared;
        if (Replay(operation) is { } replay)
            return replay;
        if (!Reference(firstResponse, FirstResponses.Schema))
            return Invalid("A root name admission requires its original frozen response.");
        RaiseEvent(new MethodologyTemplateNameClaimed(new MethodologyTemplate(intent.TemplateId).Stream, 0,
            (long)Version + 1, content, operation, firstResponse));
        return Result.Success;
    }

    public Result RefuseCreation(OperationProvenance operation, MethodologyTemplateNameClaimed claim,
        MethodologyTemplateCreationIntent intent, MethodologyTemplateCreatedV4 existing,
        MethodologyTemplateCreationConflictProofs proofs, ImmutableContentRef proofContent)
    {
        if (!RefusalBinding(operation, claim, intent, existing, proofs, proofContent))
            return Invalid("A creation refusal requires the exact admitted claim and committed incompatible line creation.");
        if (Replay(operation) is { } replay)
            return replay;
        var losingName = NameNormalization.Normalize(existing.Name) != NormalizedName;
        var release = losingName && IsHeldBy(claim.Owner, claim.Generation);
        RaiseEvent(new MethodologyTemplateCreationRefused(claim.Owner, 0, claim.Generation, claim.Intent, proofContent,
            operation, claim.Metadata.EventId, existing.Metadata.EventId, release, RefusalReason));
        return Result.Success;
    }

    public Result RefuseLegacyCreation(OperationProvenance operation, MethodologyTemplateNameClaimed claim,
        MethodologyTemplateCreationIntent intent, MethodologyTemplateCreated existing,
        MethodologyTemplateLegacyCreationConflictProofs proofs, ImmutableContentRef proofContent) =>
        Invalid($"Name {NormalizedName} legacy identity refusal is not implemented.");

    // The generic tenant-family signatures do not carry the original response or the two exact conflict proofs.
    public override Result Claim(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent) => Invalid($"Name {NormalizedName} admission requires its frozen first response.");
    public override Result Release(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent) => Invalid($"Name {NormalizedName} release requires a committed conflicting line creation.");

    void Apply(MethodologyTemplateNameClaimed fact)
    {
        if (!CreationBinding(fact.Operation, fact.Owner, fact.Intent) || fact.Revision != 0 ||
            fact.Generation != checked((long)Version + 1) || !Reference(fact.FirstResponse, FirstResponses.Schema))
            throw new InvalidOperationException("A name claim must bind its exact global line, operator and creation generation.");
        ObserveClaimed(fact);
    }

    void Apply(MethodologyTemplateCreationRefused fact)
    {
        if (!HolderScope(fact.Owner) || fact.Revision != 0 || fact.Generation <= 0 ||
            !Reference(fact.Intent, CreationSchema) || !Reference(fact.SourceProofs, ConflictSchema) ||
            fact.ClaimEventId == Uuid.Empty || fact.ExistingCreationEventId == Uuid.Empty || fact.Reason != RefusalReason ||
            !RefusalOperation(fact.Operation, fact.Owner, fact.SourceProofs, fact.ExistingCreationEventId))
            throw new InvalidOperationException("A creation refusal must preserve its exact source-bound losing claim outcome.");
        if (fact.ReleasedName)
            ObserveReleased(fact);
        else
            Acceptance.Observe(fact);
    }

    bool CreationBinding(OperationProvenance operation, EventStreamAddress holder, ImmutableContentRef intent) =>
        HolderScope(holder) && Reference(intent, CreationSchema) && operation.LogicalId != Uuid.Empty &&
        operation.RootOperationId == operation.LogicalId && operation.CausationEventId is null &&
        operation.CommandKind == CreationCommandKind && operation.Owner == Stream && operation.AffectedOwner == holder &&
        operation.ExpectedRevisionOwner == holder && (operation.FirstOwner is null || operation.FirstOwner == Stream) &&
        operation.Episode == holder.Resource && operation.OriginalExpectedRevision is null &&
        operation.AuthoredIntentSha256 == intent.Sha256 && operation.AcceptedAt != default &&
        operation.Actor.Kind == "platform_operator" && Uuid.TryParse(operation.Actor.Id, out var actor) && actor != Uuid.Empty;

    bool RefusalBinding(OperationProvenance operation, MethodologyTemplateNameClaimed claim,
        MethodologyTemplateCreationIntent intent, MethodologyTemplateCreatedV4 existing,
        MethodologyTemplateCreationConflictProofs proofs, ImmutableContentRef proofContent) =>
        ValidIntent(intent) && NameNormalization.Normalize(intent.Name) == NormalizedName &&
        claim.Owner == new MethodologyTemplate(intent.TemplateId).Stream && claim.Revision == 0 && claim.Generation > 0 &&
        claim.Metadata.AggregateId == Id && claim.Generation == (long)claim.Metadata.AggregateVersion &&
        CreationBinding(claim.Operation, claim.Owner, claim.Intent) && claim.Operation.LogicalId == intent.CommandId &&
        claim.Operation.Actor.Id == intent.CreatedBy.ToString() && Manifest(intent, claim.Intent, CreationSchema) &&
        existing.TemplateId == intent.TemplateId && existing.CreatedBy != Uuid.Empty &&
        Text(existing.Name, 200) && Text(existing.Purpose, 2000) &&
        (existing.Name != intent.Name || existing.Purpose != intent.Purpose || existing.CreatedBy != intent.CreatedBy) &&
        Proof(proofs.Claim, claim, Stream) && Proof(proofs.ExistingCreation, existing, claim.Owner) &&
        Manifest(proofs, proofContent, ConflictSchema) && operation.RootOperationId == claim.Operation.RootOperationId &&
        RefusalOperation(operation, claim.Owner, proofContent, existing.Metadata.EventId);

    bool RefusalOperation(OperationProvenance operation, EventStreamAddress holder, ImmutableContentRef proofContent,
        Uuid existingEventId) => operation.LogicalId != Uuid.Empty && operation.RootOperationId != Uuid.Empty &&
        operation.CommandKind == RefusalCommandKind && operation.Owner == Stream && operation.AffectedOwner == holder &&
        operation.ExpectedRevisionOwner == holder && (operation.FirstOwner is null || operation.FirstOwner == Stream) &&
        operation.Episode == holder.Resource && operation.OriginalExpectedRevision is null && operation.AcceptedAt != default &&
        operation.CausationEventId == existingEventId && operation.AuthoredIntentSha256 == proofContent.Sha256 &&
        operation.Actor.Kind == "system_process" && operation.Actor.Id == ProcessId;

    Result? Replay(OperationProvenance operation)
    {
        if (Acceptance.SelectedLogicalId != operation.LogicalId)
            return Invalid("The name owner must be selected for this exact logical operation.");
        return Acceptance.Decide(AcceptedOperationBinding.Of(operation)) switch
        {
            NativeAcceptanceDecision.Replayed => Result.Success,
            NativeAcceptanceDecision.Conflict => Conflict("The operation identity is already bound to another creation intent."),
            _ => null,
        };
    }

    internal static bool ValidIntent(MethodologyTemplateCreationIntent? intent) => intent is not null &&
        intent.CommandId != Uuid.Empty && intent.TemplateId != Uuid.Empty && intent.CreatedBy != Uuid.Empty &&
        Text(intent.Name, 200) && intent.Name == intent.Name.Trim() && Text(intent.Purpose, 2000) && intent.Purpose == intent.Purpose.Trim();
    static bool HolderScope(EventStreamAddress? holder) => holder is not null && holder.Realm == MethodologyTemplate.Realm &&
        holder.Area == MethodologyTemplate.Area && Uuid.TryParse(holder.Resource, out var template) && template != Uuid.Empty;
    static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.ContentId != Uuid.Empty &&
        value.Schema == schema && value.MediaType == "application/json" && value.Bytes > 0 &&
        value.Sha256 is { Length: 64 } hash && hash.All(Uri.IsHexDigit);
    static bool Text(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    static bool Manifest<T>(T value, ImmutableContentRef reference, string schema) where T : notnull
    {
        if (!Reference(reference, schema))
            return false;
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException("Methodology creation content metadata is missing.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, info);
        return bytes.LongLength == reference.Bytes && ContentDigest.Sha256(bytes) == reference.Sha256;
    }
    static bool Proof<T>(SourceFactProof<T> proof, T fact, EventStreamAddress stream) where T : DomainEvent =>
        !fact.Metadata.IsAudit && fact.Metadata.AggregateId.ToString() == stream.Resource &&
        proof.Source == stream && proof.EventId == fact.Metadata.EventId && proof.OwnerRevision == fact.Metadata.AggregateVersion &&
        proof.OwnerRevision > 0 && proof.PhysicalPosition >= proof.OwnerRevision - 1 && proof.EventKind == EventKind.Of(fact) &&
        proof.PayloadSha256 == ContentDigest.Sha256(fact, typeof(T)) && Reference(proof.RequiredFacts, SourceFactProofs.RequiredFactsSchema);
    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
