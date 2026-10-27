using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Applications.Reactors;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Tenants.Processes;

/// <summary>
///     The delivery process of one invitation, keyed by the invitation's root operation. Its root is the invitation
///     fact; the send stage performs the external effect for one immutable payload with a stable attempt identity,
///     and the record stage applies exactly one typed outcome command selected by the result the attempt stored. The
///     attempts live in the delivery record, never in the invitation's event stream.
/// </summary>
public static class TenantInvitationDeliveryProcess
{
    /// <summary>The catalog name of the process.</summary>
    public const string Name = "TenantInvitationDeliveryProcess";

    /// <summary>The registered definition; it equals the catalog entry exactly.</summary>
    public static ProcessDefinition Definition { get; } = new(Name, [nameof(TenantMemberInvited)], true,
        [
            new ProcessStageDefinition("send", [TenantInvitationDeliveryReceivers.SendTenantInvitation],
                "One immutable payload and one attempt identity; a resumed unknown result sends the same attempt."),
            new ProcessStageDefinition("record",
            [
                TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliverySent,
                TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliveryFailed,
            ], "Exactly one outcome receiver, selected by the result the attempt stored."),
        ],
        [],
        CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var content = services.GetRequiredService<IImmutableContentStore>();
        var realm = step.Record.Key.Realm;
        var payload = await content.ReadJsonAsync<TenantInvitationDeliveryPayload>(realm, step.Record.Intent, ct)
            .ConfigureAwait(false);
        var (proof, _, _) = await SourceFactProofs.BuildAsync<TenantMemberInvited>(
            services.GetRequiredService<ISourceFactResolver>(), content, realm, step.Record.Source, ct)
            .ConfigureAwait(false);
        if (step.Receiver == TenantInvitationDeliveryReceivers.SendTenantInvitation)
            return new SendTenantInvitation(step.CommandId, proof, payload.TenantId, payload.InvitationId,
                step.Record.Intent);

        var record = await services.GetRequiredService<ITenantInvitationDeliveryStore>()
            .ReadAsync(payload.TenantId, payload.InvitationId, ct).ConfigureAwait(false);
        var attempt = record?.Attempt(TenantInvitationDeliveryRecord.AttemptIdFor(payload.InvitationId));
        if (attempt is not { ResultAt: { } at } || attempt.Result == TenantInvitationDeliveryAttempt.InFlight)
            throw new SourceFactVerificationException("The delivery attempt has no stored result to record yet.");
        return (step.Receiver, attempt.Result) switch
        {
            (TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliverySent,
                TenantInvitationDeliveryAttempt.Accepted) => new RecordTenantInvitationDeliverySent(step.CommandId,
                proof, payload.TenantId, payload.InvitationId, attempt.AttemptId, at),
            (TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliveryFailed,
                TenantInvitationDeliveryAttempt.Rejected) => new RecordTenantInvitationDeliveryFailed(step.CommandId,
                proof, payload.TenantId, payload.InvitationId, attempt.AttemptId,
                attempt.FailureCode ?? "delivery_failed", at),
            _ => null,
        };
    }
}
