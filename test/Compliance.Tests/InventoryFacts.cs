using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

/// <summary>
///     Drives the inventory owners directly with a fresh native operation per call, for tests that only need a recorded
///     or revised component, asset or flow. Each call is its own logical operation, so a second creation of the same
///     identity conflicts instead of replaying, exactly as a new request with another identity would.
/// </summary>
static class InventoryFacts
{
    static readonly ImmutableContentRef Response = new(Uuid.CreateVersion4(), new string('b', 64), 2,
        "application/json", "first-response-descriptor.v1");

    static OperationProvenance Operation(EventStreamAddress owner, ActorReference actor, DateTimeOffset at,
        string commandKind, ulong? expected = null)
    {
        var id = Uuid.CreateVersion4();
        return new OperationProvenance(id, id, null, commandKind, actor, owner, expected is null ? "initial" : "current",
            expected, new string('a', 64), at, owner, owner);
    }

    public static Result<TechnologyComponentRegistration> Record(this TechnologyComponent component,
        TechnologyComponentContent content, ActorReference actor, DateTimeOffset at)
    {
        var result = component.Record(Operation(component.Stream, actor, at, "RecordTechnologyComponent"), Response,
            content, actor, at);
        return result.IsSuccess
            ? Result<TechnologyComponentRegistration>.Success(new TechnologyComponentRegistration(component.Id))
            : Result<TechnologyComponentRegistration>.Failure(result.Error!);
    }

    public static Result Revise(this TechnologyComponent component, long expectedRevision,
        Func<TechnologyComponentContent, TechnologyComponentContent> change, ActorReference actor, DateTimeOffset at) =>
        component.Revise(Operation(component.Stream, actor, at, "ReviseTechnologyComponent",
            expectedRevision >= 0 ? (ulong)expectedRevision : null), Response, expectedRevision, change, actor, at);

    public static Result<InformationAssetRegistration> Record(this InformationAsset asset,
        InformationAssetContent content, ActorReference actor, DateTimeOffset at)
    {
        var result = asset.Record(Operation(asset.Stream, actor, at, "RecordInformationAsset"), Response, content,
            actor, at);
        return result.IsSuccess
            ? Result<InformationAssetRegistration>.Success(new InformationAssetRegistration(asset.Id))
            : Result<InformationAssetRegistration>.Failure(result.Error!);
    }

    public static Result Revise(this InformationAsset asset, long expectedRevision,
        Func<InformationAssetContent, InformationAssetContent> change, ActorReference actor, DateTimeOffset at) =>
        asset.Revise(Operation(asset.Stream, actor, at, "ReviseInformationAsset",
            expectedRevision >= 0 ? (ulong)expectedRevision : null), Response, expectedRevision, change, actor, at);

    public static Result<DataFlowRegistration> Record(this DataFlow flow, DataFlowContent content,
        ActorReference actor, DateTimeOffset at)
    {
        var result = flow.Record(Operation(flow.Stream, actor, at, "RecordDataFlow"), Response, content, actor, at);
        return result.IsSuccess
            ? Result<DataFlowRegistration>.Success(new DataFlowRegistration(flow.Id))
            : Result<DataFlowRegistration>.Failure(result.Error!);
    }

    public static Result Revise(this DataFlow flow, long expectedRevision,
        Func<DataFlowContent, DataFlowContent> change, ActorReference actor, DateTimeOffset at) =>
        flow.Revise(Operation(flow.Stream, actor, at, "ReviseDataFlow",
            expectedRevision >= 0 ? (ulong)expectedRevision : null), Response, expectedRevision, change, actor, at);

    /// <summary>A component fact whose provenance nothing reads back.</summary>
    public static TechnologyComponentRevisionRecorded ComponentFact(Uuid tenantId, Uuid id, long revision,
        TechnologyComponentContent content, ActorReference actor, DateTimeOffset at) => new(tenantId, id, revision,
        content, actor, at, Operation(new TechnologyComponent(tenantId, id).Stream, actor, at,
            "RecordTechnologyComponent"), Response);

    /// <summary>An information asset fact whose provenance nothing reads back.</summary>
    public static InformationAssetRevisionRecorded AssetFact(Uuid tenantId, Uuid id, long revision,
        InformationAssetContent content, ActorReference actor, DateTimeOffset at) => new(tenantId, id, revision,
        content, actor, at, Operation(new InformationAsset(tenantId, id).Stream, actor, at,
            "RecordInformationAsset"), Response);

    /// <summary>A data flow fact whose provenance nothing reads back.</summary>
    public static DataFlowRevisionRecorded FlowFact(Uuid tenantId, Uuid id, long revision, DataFlowContent content,
        ActorReference actor, DateTimeOffset at) => new(tenantId, id, revision, content, actor, at,
        Operation(new DataFlow(tenantId, id).Stream, actor, at, "RecordDataFlow"), Response);
}
