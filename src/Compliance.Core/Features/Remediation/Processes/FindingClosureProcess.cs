using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Processes;

/// <summary>
///     The ordered finding closure process: hold every admitted action in canonical stream-key order, append the
///     closure once all are held, then settle every use as closed. The root is the closure acceptance, so the complete
///     intent exists on the finding before any action is touched; an action that is not materialized yet is a wait, not
///     a failure. A permanent proof mismatch cancels the closure through <see cref="FindingClosureFences" />, which also
///     settles every use as canceled; the abort receiver is therefore dispatched by the receivers that prove the
///     mismatch and the factory builds no command for it.
/// </summary>
static class FindingClosureProcess
{
    public const string Acquire = "acquire";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(FindingClosureEffects.ProcessName,
        [nameof(FindingClosureAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizeCorrectiveActionFindingClosureUse)],
                "Action stream keys in canonical unsigned UTF-8 order from the frozen manifest."),
            new ProcessStageDefinition(Complete, [nameof(CompleteFindingClosure)]),
            new ProcessStageDefinition(Settle, [nameof(SettleCorrectiveActionFindingClosureUse)]),
        ], [nameof(AbortFindingClosure)], Receiver);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var tenant = Uuid.Parse(record.Key.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        ClosureIntent intent;
        ClosureDecision decision;
        IReadOnlyList<AdmittedChild> children;
        try
        {
            intent = await FindingContent.ReadAsync<ClosureIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
            decision = await FindingContent.ReadAsync<ClosureDecision>(content, tenant, intent.AuthoredDecision, ct)
                .ConfigureAwait(false);
            children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(content, record.Key.Realm,
                intent.AdmittedChildren, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen closure cannot be verified yet: {exception.Message}");
        }

        var finding = new FindingAggregate(tenant, decision.ProgramId, intent.ParentId);
        var root = record.Key.RootOperationId;
        var generation = (long)intent.ParentGeneration;
        switch (step.Receiver)
        {
            case nameof(AuthorizeCorrectiveActionFindingClosureUse):
                {
                    var actionId = ActionOf(step, tenant, decision, intent, children);
                    var action = await reader.HydrateAsync(new CorrectiveAction(tenant, decision.ProgramId, intent.ParentId,
                        actionId), ct).ConfigureAwait(false);
                    // An accepted action that is not materialized yet blocks the closure until it is.
                    if (action.Latest is not { } latest)
                        throw new SourceFactVerificationException("The corrective action is not materialized yet.");
                    return new AuthorizeCorrectiveActionFindingClosureUse(step.CommandId, record.Source, action.Stream, root,
                        generation, null, record.Intent, actionId, latest.Metadata.EventId, action.Revision,
                        intent.ParentId, decision.ProgramId);
                }
            case nameof(CompleteFindingClosure):
                {
                    var held = await FindingClosureProofs.HeldAsync(reader, content, finding, intent, decision, tenant,
                        step.CommandId, ct).ConfigureAwait(false);
                    if (!held.IsSuccess)
                        throw new SourceFactVerificationException(held.Error!.Message);
                    var proofs = await FindingContent.WriteDeterministicAsync(content, tenant, step.CommandId,
                        FindingContent.ProofsSchema, held.Value!, ct).ConfigureAwait(false);
                    return new CompleteFindingClosure(step.CommandId, record.Source, finding.Stream, root, generation, null,
                        record.Intent, intent.ParentId, proofs);
                }
            case nameof(SettleCorrectiveActionFindingClosureUse):
                {
                    var closed = record.Facts.GetValueOrDefault(nameof(CompleteFindingClosure))
                        ?? throw new SourceFactVerificationException("The closed fact is not recorded on the process yet.");
                    var actionId = ActionOf(step, tenant, decision, intent, children);
                    return new SettleCorrectiveActionFindingClosureUse(step.CommandId, closed,
                        new CorrectiveAction(tenant, decision.ProgramId, intent.ParentId, actionId).Stream, root, generation,
                        null, record.Intent, actionId, intent.ParentId, decision.ProgramId,
                        Bdgrz.Compliance.Features.SourceUse.Models.SourceUseSettlementDisposition.ParentClosed);
                }
            case nameof(AbortFindingClosure):
                return null;
            default:
                throw new InvalidOperationException($"{step.Receiver} is not a finding closure receiver.");
        }
    }

    static Uuid ActionOf(ProcessStep step, Uuid tenant, ClosureDecision decision, ClosureIntent intent,
        IReadOnlyList<AdmittedChild> children) =>
        children.FirstOrDefault(child => FindingClosureEffects.ActionKey(tenant, decision.ProgramId, intent.ParentId,
            child.ChildId) == step.Target)?.ChildId
        ?? throw new InvalidOperationException($"{step.Target} is not an action of the frozen closure.");
}
