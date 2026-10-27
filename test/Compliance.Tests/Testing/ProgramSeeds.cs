using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

static class ProgramSeeds
{
    public static Task<DomainEvent> CreatedAsync(IImmutableContentStore content, Uuid tenantId, Uuid programId,
        string name, ProgramPlan plan, Uuid member, string display, DateTimeOffset at) =>
        FreezeAsync(content, tenantId, programId, new ProgramRuleChange(nameof(CreateProgram), 1,
            new ProgramContent(name, plan), null, ActorReference.ForMember(member, display), at), 1);

    public static ProgramOperations Operations(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, TimeProvider? clock = null)
    {
        var verified = new ProgramAggregateReader(reader, events);
        return new ProgramOperations(new NativeOperationExecutor(verified, new AggregateExecutor(verified, writer), content),
            content, clock ?? TimeProvider.System);
    }

    public static async Task<ComplianceProgram> OwnerAsync(IImmutableContentStore content, ProgramRules rules)
    {
        var owner = new ComplianceProgram(rules.TenantId, rules.Id);
        new AggregateScenario<ComplianceProgram>(owner).Given((await FactsAsync(content, rules)).ToArray());
        return owner;
    }

    public static Task WriteAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, ProgramRules rules) => SaveAsync(reader, writer, events, content, rules, false);

    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, ProgramRules rules) => SaveAsync(reader, writer, events, content, rules, true);

    static async Task SaveAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, ProgramRules rules, bool append)
    {
        foreach (var change in (append ? rules.PendingChanges : rules.AppliedChanges).ToArray())
        {
            var logical = change.Kind == nameof(CreateProgram) ? rules.Id : Uuid.CreateVersion4();
            var owner = await reader.HydrateAsync(new ComplianceProgram(rules.TenantId, rules.Id, logical));
            _ = await ProgramHistory.ReadAsync(events, owner);
            var frozen = await FreezeAsync(content, rules.TenantId, rules.Id, change, owner.NativeSequence + 1,
                owner.CurrentContent, logical);
            var result = frozen switch
            {
                ProgramCreated fact => owner.Create(fact.Operation, fact.Content, fact.FirstResponse!, fact.OperationResult!),
                ProgramRevised fact => owner.Revise(fact.Operation, fact.Content, fact.FirstResponse!, fact.OperationResult!),
                ProgramCriteriaEditionSelected fact => owner.SelectCriteriaEdition(fact.Operation, fact.EditionId, fact.FirstResponse!, fact.OperationResult!),
                _ => throw new InvalidOperationException("Another program fixture fact."),
            };
            Assert.True(result.IsSuccess, result.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
        rules.ClearPendingChanges();
    }

    public static async Task<IReadOnlyList<DomainEvent>> FactsAsync(IImmutableContentStore content, ProgramRules rules)
    {
        var result = new List<DomainEvent>();
        Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef? current = null;
        foreach (var change in rules.AppliedChanges)
        {
            var fact = await FreezeAsync(content, rules.TenantId, rules.Id, change, (ulong)result.Count + 1, current);
            current = ((IProgramFact)fact).Content;
            result.Add(fact);
        }
        return result;
    }

    public static async Task<DomainEvent> FreezeAsync(IImmutableContentStore content, Uuid tenantId, Uuid programId,
        ProgramRuleChange change, ulong sequence, Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef? current = null,
        Uuid logicalId = default)
    {
        var logical = logicalId != Uuid.Empty ? logicalId : change.Kind == nameof(CreateProgram) ? programId : Uuid.CreateVersion4();
        object request = change.Kind switch
        {
            nameof(CreateProgram) => new CreateProgram(programId, tenantId, change.Content!.Name, change.Content.Plan),
            nameof(ReviseProgram) => new ReviseProgram(logical, tenantId, programId, change.Revision - 1, change.Content!.Name, change.Content.Plan),
            nameof(SelectProgramCriteriaEdition) => new SelectProgramCriteriaEdition(logical, tenantId, programId, change.Revision - 1, change.EditionId!.Value),
            _ => throw new InvalidOperationException("Another program fixture change."),
        };
        var owner = new ComplianceProgram(tenantId, programId);
        var operation = OwnerProvenance.For(owner, logical, change.Kind, change.Actor, RootOperation.InitialEpisode,
            change.Kind == nameof(CreateProgram) ? null : unchecked((ulong)(change.Revision - 1)), IntentDigest(request),
            change.ChangedAt) with
        { FirstOwner = owner.Stream };
        var authored = change.Content is { } value
            ? await NativeContent.WriteAsync(content, tenantId, ProgramContent.Schema, value) : current!;
        var response = change.Kind == nameof(CreateProgram)
            ? await NativeContent.FreezeAsync(content, tenantId, ProgramOperations.RegistrationSchema, new ProgramRegistration(programId))
            : await NativeContent.FreezeUnitAsync(content, tenantId);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), response.Response);
        return change.Kind switch
        {
            nameof(CreateProgram) => new ProgramCreated(tenantId, programId, change.Revision, authored, change.ChangedAt, operation,
                first, response.Result, sequence, 1),
            nameof(ReviseProgram) => new ProgramRevised(tenantId, programId, change.Revision, authored, change.ChangedAt, operation,
                first, response.Result, sequence, 1),
            _ => new ProgramCriteriaEditionSelected(tenantId, programId, change.Revision, change.EditionId!.Value, authored,
                change.ChangedAt, operation, first, response.Result, sequence, 1),
        };
    }
    static string IntentDigest(object intent) => intent switch
    {
        CreateProgram value => AuthoredIntentDigest.Compute(value),
        ReviseProgram value => AuthoredIntentDigest.Compute(value),
        SelectProgramCriteriaEdition value => AuthoredIntentDigest.Compute(value),
        _ => throw new InvalidOperationException("Another program authored intent."),
    };

}
