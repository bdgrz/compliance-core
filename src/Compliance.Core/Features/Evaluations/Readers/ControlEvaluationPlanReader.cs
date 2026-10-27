using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Evaluations.Readers;

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
