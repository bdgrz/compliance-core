using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Programs.Readers;

public sealed class ProgramReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async Task<ProgramHistorySnapshot> HistoryAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ComplianceProgram(tenantId, programId), ct).ConfigureAwait(false);
        return await ProgramHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
    }

    public async Task<ProgramRules> RulesAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default)
    {
        var history = await HistoryAsync(tenantId, programId, ct).ConfigureAwait(false);
        var rules = new ProgramRules(tenantId, programId);
        foreach (var fact in history.Facts.OfType<IProgramFact>())
        {
            if (fact.ChangeCount == 0)
                continue;
            var member = Uuid.Parse(fact.Operation.Actor.Id, CultureInfo.InvariantCulture);
            var display = fact.Operation.Actor.Display;
            var authored = await ReadContentAsync(content, fact, ct).ConfigureAwait(false);
            var failure = fact switch
            {
                ProgramCreated => rules.Create(authored.Name, authored.Plan, member, display, fact.Operation.AcceptedAt),
                ProgramRevised revised => rules.Revise(revised.Revision - 1, authored.Name, authored.Plan, member, display, revised.ChangedAt),
                ProgramCriteriaEditionSelected selected => rules.SelectCriteriaEdition(selected.Revision - 1, selected.EditionId,
                    member, display, selected.ChangedAt),
                _ => throw new ImmutableContentIntegrityException("The program has another history fact kind."),
            };
            if (failure is not null)
                throw new ImmutableContentIntegrityException("The program authored value violates its recorded business transition.");
        }
        rules.ClearPendingChanges();
        return rules;
    }

    public static async Task<ProgramContent> ReadContentAsync(IImmutableContentStore content, IProgramFact fact,
        CancellationToken ct = default)
    {
        ProgramHistory.ValidateHeader(fact);
        var value = await ReadContentAsync(content, fact.TenantId, fact.Content, ct).ConfigureAwait(false);
        var expected = fact.Operation.OriginalExpectedRevision;
        var digest = fact switch
        {
            ProgramCreated => AuthoredIntentDigest.Compute(new CreateProgram(fact.ProgramId, fact.TenantId, value.Name, value.Plan)),
            ProgramRevised => AuthoredIntentDigest.Compute(new ReviseProgram(fact.Operation.LogicalId, fact.TenantId, fact.ProgramId,
                (long)expected!.Value, value.Name, value.Plan)),
            ProgramCriteriaEditionSelected selected => AuthoredIntentDigest.Compute(new SelectProgramCriteriaEdition(fact.Operation.LogicalId,
                fact.TenantId, fact.ProgramId, (long)expected!.Value, selected.EditionId)),
            _ => throw new ImmutableContentIntegrityException("The program has another authored value kind."),
        };
        if (digest != fact.Operation.AuthoredIntentSha256)
            throw new ImmutableContentIntegrityException("The program authored value differs from its exact native command binding.");
        return value;
    }

    public static async Task<ProgramContent> ReadContentAsync(IImmutableContentStore content, Uuid tenantId,
        Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference, CancellationToken ct = default)
    {
        if (reference.Schema != ProgramContent.Schema || reference.MediaType != ImmutableContentStoreExtensions.JsonMediaType)
            throw new ImmutableContentIntegrityException("The program authored value has another schema or media type.");
        var value = await content.ReadJsonAsync<ProgramContent>(tenantId.ToString(), reference, ct).ConfigureAwait(false);
        if (ProgramRules.Validate(value) is not null || value.Name != value.Name.Trim())
            throw new ImmutableContentIntegrityException("The program authored value is malformed or not canonical.");
        return value;
    }
}
