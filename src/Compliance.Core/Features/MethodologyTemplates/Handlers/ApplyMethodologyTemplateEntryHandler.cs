using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Realizes verified entry content through destination authorization and records only its actual owner answer.</summary>
public sealed class ApplyMethodologyTemplateEntryHandler(IAggregateExecutor executor, IAggregateReader reader,
    ISourceFactResolver sources, IImmutableContentStore content, IRequestBus bus, ControlReader controls, PolicyReader policies)
    : IRequestHandler<ApplyMethodologyTemplateEntry>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyMethodologyTemplateEntry> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a committed methodology application reaction may realize an entry."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TemplateId == Uuid.Empty ||
            request.TemplateVersion <= 0 || string.IsNullOrWhiteSpace(request.EntryKey) || request.Source is null ||
            request.Source.Stream != new TemplateApplication(request.TenantId, request.ProgramId, request.TemplateId, true).Stream ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, "apply-template-entry:" + request.EntryKey))
            return Invalid();
        var (_, source, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateAppliedV4>(sources, content,
            request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (source.TenantId != request.TenantId || source.ProgramId != request.ProgramId || source.TemplateId != request.TemplateId ||
            source.TemplateVersion != request.TemplateVersion || source.AppliedBy == Uuid.Empty ||
            source.EntriesToCreate.Concat(source.EntriesToPropose).Count(key => key == request.EntryKey) != 1)
            return Invalid();
        var entries = await content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm,
            source.EntriesContent, ct).ConfigureAwait(false);
        if (!MethodologyTemplateDraft.ValidManifest(entries, source.EntriesContent))
            return Invalid();
        var entry = entries.SingleOrDefault(item => item.EntryKey == request.EntryKey);
        if (entry is null)
            return Invalid();
        var version = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        if (!version.IsOpened)
            return Pending("The version application is not open yet.");
        if (version.ApplicationEventId != request.Source.EventId || version.ApplicationCommandId != source.CommandId)
            return Invalid();
        if (version.OutcomeFor(request.EntryKey) is not null)
            return Result.Success;

        var planned = MethodologyTemplateDestinationCommands.Create(request.TenantId, request.ProgramId, entry,
            request.TemplateId, request.TemplateVersion);
        if (!planned.IsSuccess)
            return await RecordAsync(context, Refusal(request.EntryKey, planned.Error!), ct).ConfigureAwait(false);
        var proposal = source.EntriesToPropose.Contains(request.EntryKey, StringComparer.Ordinal);
        if (proposal)
        {
            var selected = version.PreparedProposalFor(request.EntryKey);
            if (selected is null)
            {
                selected = await SelectProposalBaseAsync(request, planned.Value, ct).ConfigureAwait(false);
                if (selected is null)
                    return await RecordAsync(context, new TemplateEntryOutcome(request.EntryKey, "refused", null,
                        "approved_destination_revision_required"), ct).ConfigureAwait(false);
                var candidate = selected;
                var prepared = await executor.ExecuteAsync(Owner(request), owner => AggregateOutcome.CommitOnSuccess(
                    owner.PreparedProposalFor(request.EntryKey) is null ? owner.PrepareProposal(candidate) : Result.Success),
                    context, ct).ConfigureAwait(false);
                if (!prepared.IsSuccess)
                    return prepared;
                version = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
                selected = version.PreparedProposalFor(request.EntryKey);
                if (selected is null)
                    return Pending("The selected proposal base is not visible yet.");
            }
            planned = MethodologyTemplateDestinationCommands.Propose(request.CommandId, request.TenantId, request.ProgramId,
                entry, selected.RecordId, selected.ApprovedVersionId, selected.ApprovedRevision, null,
                request.TemplateId, request.TemplateVersion);
            if (!planned.IsSuccess)
                return await RecordAsync(context, Refusal(request.EntryKey, planned.Error!), ct).ConfigureAwait(false);
        }

        var destination = planned.Value;
        var acceptance = await ReadAcceptanceAsync(request, destination, ct).ConfigureAwait(false);
        var binding = Binding(request, source.AppliedBy, destination, acceptance.Stream);
        var decision = acceptance.Acceptance.Decide(binding);
        if (decision == NativeAcceptanceDecision.Conflict)
            return await RecordAsync(context, new TemplateEntryOutcome(request.EntryKey, "refused", null,
                "identifier_in_use"), ct).ConfigureAwait(false);
        if (decision == NativeAcceptanceDecision.None)
        {
            // The principal comes only from the verified committed admission. Destination authorizers
            // still check current membership and permission; no actor supplied by the reaction is trusted.
            var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
                new Claim("sub", source.AppliedBy.ToString())], "VerifiedMethodologyApplication"));
            var delegated = bus.CreateContext(actor, RequestMetadata.FromRequest(destination.Request, context));
            var applied = await DispatchAsync(destination.Request, delegated, ct).ConfigureAwait(false);
            if (!applied.IsSuccess)
                return applied.Error!.IsTransient ? applied
                    : await RecordAsync(context, Refusal(request.EntryKey, applied.Error), ct).ConfigureAwait(false);
            acceptance = await ReadAcceptanceAsync(request, destination, ct).ConfigureAwait(false);
            if (acceptance.Acceptance.Decide(binding) != NativeAcceptanceDecision.Replayed)
                return Pending("The destination's exact accepted operation is not visible yet.");
        }
        if (acceptance.Acceptance.SelectedFact is not { } accepted)
            return Pending("The destination's exact accepted fact is not visible yet.");
        return await RecordAsync(context, new TemplateEntryOutcome(request.EntryKey, proposal ? "proposed" : "created",
            destination.RecordId, null, NativeFactReference.Of(acceptance.Stream, accepted)), ct).ConfigureAwait(false);
    }

    async ValueTask<TemplateEntryProposalBase?> SelectProposalBaseAsync(ApplyMethodologyTemplateEntry request,
        MethodologyTemplateDestinationCommand destination, CancellationToken ct)
    {
        if (destination.RecordKind == "control")
        {
            var current = await controls.ReadAsync(request.TenantId, request.ProgramId, destination.RecordId, ct).ConfigureAwait(false);
            return current.ApprovedVersion is { } version ? new TemplateEntryProposalBase(request.EntryKey, "control",
                destination.RecordId, version.VersionId, null) : null;
        }
        if (destination.RecordKind == "policy")
        {
            var current = await policies.ReadAsync(request.TenantId, request.ProgramId, destination.RecordId, ct).ConfigureAwait(false);
            return current.CurrentVersion is { } version ? new TemplateEntryProposalBase(request.EntryKey, "policy",
                destination.RecordId, null, version.Version) : null;
        }
        return null;
    }

    async ValueTask<DestinationAcceptance> ReadAcceptanceAsync(ApplyMethodologyTemplateEntry request,
        MethodologyTemplateDestinationCommand destination, CancellationToken ct)
    {
        var command = ((IIdentifiedRequest)destination.Request).RequestId;
        switch (destination.RecordKind)
        {
            case "control":
                var control = await reader.HydrateAsync(new ControlDraft(request.TenantId, destination.RecordId, command), ct).ConfigureAwait(false);
                return new DestinationAcceptance(control.Stream, control.Acceptance);
            case "policy":
                var policy = await reader.HydrateAsync(new Policy(request.TenantId, destination.RecordId, command), ct).ConfigureAwait(false);
                return new DestinationAcceptance(policy.Stream, policy.Acceptance);
            case "risk":
                var risk = await reader.HydrateAsync(new RiskDraft(request.TenantId, destination.RecordId, command), ct).ConfigureAwait(false);
                return new DestinationAcceptance(risk.Stream, risk.Acceptance);
            default:
                throw new InvalidOperationException("A methodology destination must be a control, policy or risk owner.");
        }
    }

    static AcceptedOperationBinding Binding(ApplyMethodologyTemplateEntry request, Uuid user,
        MethodologyTemplateDestinationCommand destination, EventStreamAddress stream)
    {
        var command = ((IIdentifiedRequest)destination.Request).RequestId;
        var (kind, digest, expected) = destination.Request switch
        {
            CreateControlDraft value => (nameof(CreateControlDraft), AuthoredIntentDigest.Compute(value with
            { Identifier = ControlDraft.NormalizeIdentifier(value.Identifier), Content = ControlRules.NormalizeContent(value.Content) }), (ulong?)null),
            ProposeControlSuccessor value => (nameof(ProposeControlSuccessor), AuthoredIntentDigest.Compute(value), (ulong?)null),
            CreatePolicyDraft value => (nameof(CreatePolicyDraft), AuthoredIntentDigest.Compute(value), (ulong?)null),
            ProposePolicySuccessor value => (nameof(ProposePolicySuccessor), AuthoredIntentDigest.Compute(value), (ulong?)value.ExpectedVersion),
            CreateRiskDraft value => (nameof(CreateRiskDraft), AuthoredIntentDigest.Compute(value), (ulong?)null),
            _ => throw new InvalidOperationException("The methodology destination command has no native acceptance contract."),
        };
        return new AcceptedOperationBinding(request.TenantId.ToString(), command, kind, stream, stream, stream,
            destination.RecordId.ToString(), ActorReference.ForMember(RbacIds.Member(request.TenantId, user), string.Empty), expected, digest);
    }

    async ValueTask<Result> DispatchAsync(IRequestBase request, RequestDispatchContext context, CancellationToken ct)
    {
        switch (request)
        {
            case CreateControlDraft value:
                var control = await bus.DispatchAsync(value, context, ct).ConfigureAwait(false);
                return control.IsSuccess ? Result.Success : Result.Failure(control.Error!);
            case ProposeControlSuccessor value:
                var successor = await bus.DispatchAsync(value, context, ct).ConfigureAwait(false);
                return successor.IsSuccess ? Result.Success : Result.Failure(successor.Error!);
            case CreatePolicyDraft value:
                var policy = await bus.DispatchAsync(value, context, ct).ConfigureAwait(false);
                return policy.IsSuccess ? Result.Success : Result.Failure(policy.Error!);
            case ProposePolicySuccessor value:
                var policySuccessor = await bus.DispatchAsync(value, context, ct).ConfigureAwait(false);
                return policySuccessor.IsSuccess ? Result.Success : Result.Failure(policySuccessor.Error!);
            case CreateRiskDraft value:
                var risk = await bus.DispatchAsync(value, context, ct).ConfigureAwait(false);
                return risk.IsSuccess ? Result.Success : Result.Failure(risk.Error!);
            default:
                return Result.Failure(new RequestError(RequestErrorKind.Validation, "The destination command is unsupported."));
        }
    }

    ValueTask<Result> RecordAsync(IRequestContext<ApplyMethodologyTemplateEntry> context, TemplateEntryOutcome outcome,
        CancellationToken ct) => executor.ExecuteAsync(Owner(context.Request), owner => AggregateOutcome.CommitOnSuccess(
            owner.ApplicationEventId == context.Request.Source.EventId ? owner.Record(outcome) : Invalid()), context, ct);

    static TemplateVersionApplication Owner(ApplyMethodologyTemplateEntry request) =>
        new(request.TenantId, request.ProgramId, request.TemplateId, request.TemplateVersion);

    static TemplateEntryOutcome Refusal(string key, RequestError error)
    {
        var reason = error.Message.Trim();
        if (reason.Length == 0)
            reason = error.Kind.ToString();
        return new TemplateEntryOutcome(key, "refused", null, reason.Length > 2000 ? reason[..2000] : reason);
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Entry application requires its exact committed canonical source, original actor and immutable planned entry."));
    static Result Pending(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, isTransient: true));
    sealed record DestinationAcceptance(EventStreamAddress Stream, NativeAcceptance Acceptance);
}
