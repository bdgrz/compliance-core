using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>The authoritative holder of one normalized platform-global methodology name.</summary>
public sealed class MethodologyTemplateNameClaim(string name, Uuid selectedLogicalId = default)
    : NameClaim(Identity(name), AddressOf(name), selectedLogicalId)
{
    public const string Area = "methodology-template-names";
    public const string CreationSchema = "methodology-template-creation-intent.v1";
    public const string ConflictSchema = "methodology-template-creation-conflict.v1";
    public const string CreationCommandKind = "CreateMethodologyTemplateV3";
    public const string RefusalCommandKind = "RefuseMethodologyTemplateCreation";
    public const string ProcessId = "methodology-template-creation";
    public string NormalizedName { get; } = NameNormalization.Normalize(name);
    public static Uuid Identity(string name) => AggregateIdentity.Create(nameof(MethodologyTemplateNameClaim), MethodologyTemplate.Realm, NameNormalization.Normalize(name));
    public static EventStreamAddress AddressOf(string name) => new(MethodologyTemplate.Realm, Area, Identity(name).ToString());
    public Result PrepareCreation(OperationProvenance operation, EventStreamAddress holder, ImmutableContentRef intent) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, $"Name {NormalizedName} admission is not implemented."));
    public Result ClaimCreation(OperationProvenance operation, EventStreamAddress holder, ImmutableContentRef intent,
        ImmutableContentRef firstResponse) => PrepareCreation(operation, holder, intent);
    public Result RefuseCreation(OperationProvenance operation, MethodologyTemplateNameClaimed claim,
        MethodologyTemplateCreationIntent intent, MethodologyTemplateCreatedV4 existing,
        MethodologyTemplateCreationConflictProofs proofs, ImmutableContentRef proofContent) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, $"Name {NormalizedName} refusal is not implemented."));
    public override Result Claim(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent) => PrepareCreation(operation, holder, intent);
    public override Result Release(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent) => PrepareCreation(operation, holder, intent);
}
