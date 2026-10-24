using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The ordered request resolution process: drain every admitted comment and sample selection, hold every admitted
///     response in canonical stream-key order, append the resolution once all are held, then settle every use as resolved.
///     The root is the resolution acceptance, so the complete intent exists on the request before any response is
///     touched; a response that is not materialized yet is a wait, not a failure. A permanent proof mismatch cancels the
///     resolution through <see cref="AuditorRequestResolutionFences" />, which also settles every use as canceled; the
///     abort receiver is therefore dispatched by the receivers that prove the mismatch and the factory builds no command
///     for it.
/// </summary>
static class AuditorRequestResolutionProcess
{
    public const string Acquire = "acquire";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(AuditorRequestResolutionEffects.ProcessName,
        [nameof(AuditorRequestResolutionAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizeAuditorResponseRequestResolutionUse)],
                "Response stream keys in canonical unsigned UTF-8 order from the frozen manifest."),
            new ProcessStageDefinition(Complete, [nameof(CompleteAuditorRequestResolution)]),
            new ProcessStageDefinition(Settle, [nameof(SettleAuditorResponseRequestResolutionUse)]),
        ], [nameof(AbortAuditorRequestResolution)], Receiver);

    /// <summary>The canonical response keys of a frozen resolution: the targets of its acquire and settle stages.</summary>
    public static IEnumerable<string> ResponseKeys(Uuid tenant, Uuid programId, Uuid requestId,
        IEnumerable<AdmittedRequestChild> children) =>
        children.Where(static child => child.Kind == AdmittedRequestChildKind.Response).Select(child =>
            AuditorRequestResolutionEffects.ResponseKey(tenant, programId, requestId, child.ChildId));

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var tenant = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        ClosureIntent intent;
        AuditorResolutionDecision decision;
        IReadOnlyList<AdmittedRequestChild> children;
        try
        {
            intent = await AuditorContent.ReadAsync<ClosureIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
            decision = await AuditorContent.ReadAsync<AuditorResolutionDecision>(content, tenant, intent.AuthoredDecision,
                ct).ConfigureAwait(false);
            children = await AuditorRequestResolutionProofs.ChildrenAsync(content, tenant, intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactVerificationException($"The frozen resolution cannot be verified yet: {exception.Message}");
        }

        var request = new AuditorRequest(tenant, decision.ProgramId, intent.ParentId);
        var root = record.Key.RootOperationId;
        var generation = (long)intent.ParentGeneration;
        switch (step.Receiver)
        {
            case nameof(AuthorizeAuditorResponseRequestResolutionUse):
                {
                    // Resolution drains the admitted comments and selections before the final response use collection.
                    var drained = await AuditorRequestResolutionProofs.DrainedAsync(reader, children, tenant,
                        decision.ProgramId, intent.ParentId, ct).ConfigureAwait(false);
                    if (!drained.IsSuccess)
                        throw new SourceFactVerificationException(drained.Error!.Message);
                    var responseId = ResponseOf(step, tenant, decision, intent, children);
                    var response = await reader.HydrateAsync(new AuditorResponse(tenant, decision.ProgramId,
                        intent.ParentId, responseId), ct).ConfigureAwait(false);
                    // An accepted response that is not materialized yet blocks the resolution until it is.
                    if (response.Latest is not { } latest)
                        throw new SourceFactVerificationException("The auditor response is not materialized yet.");
                    return new AuthorizeAuditorResponseRequestResolutionUse(step.CommandId, record.Source,
                        response.Stream, root, generation, null, record.Intent, responseId, latest.Metadata.EventId,
                        (ulong)response.Revision, intent.ParentId, decision.ProgramId);
                }
            case nameof(CompleteAuditorRequestResolution):
                {
                    var drained = await AuditorRequestResolutionProofs.DrainedAsync(reader, children, tenant,
                        decision.ProgramId, intent.ParentId, ct).ConfigureAwait(false);
                    if (!drained.IsSuccess)
                        throw new SourceFactVerificationException(drained.Error!.Message);
                    var held = await AuditorRequestResolutionProofs.HeldAsync(reader, children, intent, decision, tenant,
                        intent.ParentId, step.CommandId, ct).ConfigureAwait(false);
                    if (!held.IsSuccess)
                        throw new SourceFactVerificationException(held.Error!.Message);
                    var proofs = await AuditorContent.WriteDeterministicAsync(content, tenant, step.CommandId,
                        AuditorContent.ProofsSchema, held.Value!, ct).ConfigureAwait(false);
                    return new CompleteAuditorRequestResolution(step.CommandId, record.Source, request.Stream, root,
                        generation, null, record.Intent, intent.ParentId, proofs);
                }
            case nameof(SettleAuditorResponseRequestResolutionUse):
                {
                    var resolved = record.Facts.GetValueOrDefault(nameof(CompleteAuditorRequestResolution))
                        ?? throw new SourceFactVerificationException("The resolved fact is not recorded on the process yet.");
                    var responseId = ResponseOf(step, tenant, decision, intent, children);
                    return new SettleAuditorResponseRequestResolutionUse(step.CommandId, resolved,
                        new AuditorResponse(tenant, decision.ProgramId, intent.ParentId, responseId).Stream, root,
                        generation, null, record.Intent, responseId, intent.ParentId, decision.ProgramId,
                        SourceUse.SourceUseSettlementDisposition.ParentClosed);
                }
            case nameof(AbortAuditorRequestResolution):
                return null;
            default:
                throw new InvalidOperationException($"{step.Receiver} is not a request resolution receiver.");
        }
    }

    static Uuid ResponseOf(ProcessStep step, Uuid tenant, AuditorResolutionDecision decision, ClosureIntent intent,
        IReadOnlyList<AdmittedRequestChild> children) =>
        children.FirstOrDefault(child => child.Kind == AdmittedRequestChildKind.Response &&
            AuditorRequestResolutionEffects.ResponseKey(tenant, decision.ProgramId, intent.ParentId, child.ChildId) ==
            step.Target)?.ChildId
        ?? throw new InvalidOperationException($"{step.Target} is not a response of the frozen resolution.");
}
