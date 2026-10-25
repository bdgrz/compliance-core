using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

static class MappingRuleFacts
{
    public static OperationProvenance Root(ControlCriterionMappingRules rules, Uuid id, string kind, long? revision,
        Uuid? actor = null, DateTimeOffset? at = null) => Root(rules.Stream, id, kind, revision, actor, at);
    public static OperationProvenance Root(CriterionApplicabilityRules rules, Uuid id, string kind, long? revision,
        Uuid? actor = null, DateTimeOffset? at = null) => Root(rules.Stream, id, kind, revision, actor, at);
    static OperationProvenance Root(EventStreamAddress stream, Uuid id, string kind, long? revision, Uuid? actor,
        DateTimeOffset? at) => RootOperation.Provenance(id, kind, stream,
        ActorReference.ForMember(actor ?? Uuid.CreateVersion4(), "Tester"), revision, new string('b', 64), at ?? NativeFacts.Now);

    public static object[] Raised(ControlCriterionMappingRules rules) => RoundTrip(rules.Changes);
    public static object[] Raised(CriterionApplicabilityRules rules) => RoundTrip(rules.Changes);
    static object[] RoundTrip(IReadOnlyList<object> changes) => [.. changes.Select(change =>
        JsonSerializer.Deserialize(JsonSerializer.Serialize(change, change.GetType(), ComplianceCoreJsonContext.Default),
            change.GetType(), ComplianceCoreJsonContext.Default)!)];
    public static ControlCriterionMappingRules Replay(ControlCriterionMappingRules fresh, IEnumerable<object> changes)
    { foreach (var change in changes) fresh.Apply(change); return fresh; }
    public static CriterionApplicabilityRules Replay(CriterionApplicabilityRules fresh, IEnumerable<object> changes)
    { foreach (var change in changes) fresh.Apply(change); return fresh; }
}
