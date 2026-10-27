using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Verified full plan-version composition outside compact command authority.</summary>
public static class ControlEvaluationPlanReader
{
    public const string VersionSchema = "control-evaluation-plan-version.v2";

    public static async Task<IReadOnlyList<ControlEvaluationPlanVersionView>> ReadAsync(IAggregateReader reader,
        IDomainEventReader events, IImmutableContentStore content, Uuid tenant, Uuid program, Uuid control,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ControlEvaluationPlan(tenant, program, control), ct).ConfigureAwait(false);
        if (owner.ProgramId != program)
            return [];
        var facts = await NativeOwnerHistory.ReadAsync<ControlEvaluationPlanVersionDefined>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The plan history differs from its hydrated exact native cut.");
        var rules = new ControlEvaluationPlanRules(tenant, program, control);
        foreach (var fact in facts)
        {
            var version = await ReadVersionAsync(content, fact, ct).ConfigureAwait(false);
            if (version.TenantId != tenant || version.ProgramId != program || version.ControlId != control ||
                version.Version != rules.Revision + 1 || rules.Define(rules.Revision, version.ControlVersionId,
                    version.PlanVersionId, version.Objective, version.Steps, version.TesterIndependenceRequired,
                    version.AuthoredByMemberId, version.Author, version.DefinedAt) is not null ||
                ContentDigest.OfJson(version) != ContentDigest.OfJson(rules.CurrentVersion!))
                throw new ImmutableContentIntegrityException("The plan body violates its original source scope, sequence or content rules.");
        }
        return rules.ReadVersions();
    }

    public static async Task<ControlEvaluationPlanVersionView> ReadVersionAsync(IImmutableContentStore content,
        ControlEvaluationPlanVersionDefined fact, CancellationToken ct = default)
    {
        var owner = new ControlEvaluationPlan(fact.TenantId, fact.ProgramId, fact.ControlId);
        if (fact.Content is null || fact.Operation is null || fact.Operation.Actor is null || fact.Content.Schema != VersionSchema || fact.Version < 1 || fact.PlanVersionId != fact.Operation.LogicalId ||
            fact.FirstResponse is null || fact.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(DefineControlEvaluationPlan), (ulong)(fact.Version - 1)))
            throw new ImmutableContentIntegrityException("The plan version has another exact native source root.");
        var version = await content.ReadJsonAsync<ControlEvaluationPlanVersionView>(fact.TenantId.ToString(), fact.Content, ct)
            .ConfigureAwait(false);
        if (version.Author is null || version.Author.Kind != "member" || version.TenantId != fact.TenantId || version.ProgramId != fact.ProgramId || version.ControlId != fact.ControlId ||
            version.ControlVersionId != fact.ControlVersionId || version.PlanVersionId != fact.PlanVersionId ||
            version.Version != fact.Version || version.Author != fact.Operation.Actor ||
            version.Author.Id != version.AuthoredByMemberId.ToString() || version.DefinedAt != fact.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The plan body differs from its accepted immutable source version.");
        var rules = new ControlEvaluationPlanRules(version.TenantId, version.ProgramId, version.ControlId);
        if (rules.Define(0, version.ControlVersionId, version.PlanVersionId, version.Objective, version.Steps,
                version.TesterIndependenceRequired, version.AuthoredByMemberId, version.Author, version.DefinedAt) is not null ||
            ContentDigest.OfJson(rules.CurrentVersion! with { Version = version.Version }) != ContentDigest.OfJson(version))
            throw new ImmutableContentIntegrityException("The immutable plan body violates its original complete content rules.");
        return version;
    }
}
