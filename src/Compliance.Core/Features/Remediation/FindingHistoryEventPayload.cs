using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

sealed record FindingHistoryEventPayload(string Kind, long Revision,
    DateTimeOffset OccurredAt, JsonElement Json);

static class FindingHistoryEventPayloads
{
    public static bool TryIdentify(DomainEvent domainEvent, Uuid tenantId, Uuid programId,
        out Uuid findingId, out FindingHistoryEventPayload payload)
    {
        findingId = Uuid.Empty;
        payload = null!;
        switch (domainEvent)
        {
            case FindingRaised ev when InProgram(ev.TenantId, ev.ProgramId, tenantId, programId):
                findingId = ev.FindingId;
                break;
            case FindingRevised ev when InProgram(ev.TenantId, ev.ProgramId, tenantId, programId):
                findingId = ev.FindingId;
                break;
            case CorrectiveActionAdded ev when InProgram(ev.TenantId, ev.ProgramId,
                    tenantId, programId):
                findingId = ev.FindingId;
                break;
            case CorrectiveActionCompleted ev when InProgram(ev.TenantId, ev.ProgramId,
                    tenantId, programId):
                findingId = ev.FindingId;
                break;
            case FindingAcceptanceLinked ev when InProgram(ev.TenantId, ev.ProgramId,
                    tenantId, programId):
                findingId = ev.FindingId;
                break;
            case FindingClosed ev when InProgram(ev.TenantId, ev.ProgramId,
                    tenantId, programId):
                findingId = ev.FindingId;
                break;
            case FindingReopened ev when InProgram(ev.TenantId, ev.ProgramId,
                    tenantId, programId):
                findingId = ev.FindingId;
                break;
            default:
                return false;
        }

        return findingId != Uuid.Empty && TryCreate(domainEvent, tenantId, programId,
            findingId, out payload);
    }

    public static bool TryCreate(DomainEvent domainEvent, Uuid tenantId, Uuid programId,
        Uuid findingId, out FindingHistoryEventPayload payload)
    {
        payload = null!;
        switch (domainEvent)
        {
            case FindingRaised ev when Scope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId):
                payload = new("finding_raised", ev.Revision, ev.RaisedAt,
                    JsonSerializer.SerializeToElement(ev, ComplianceCoreJsonContext.Default.FindingRaised));
                return true;
            case FindingRevised ev when Scope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId):
                payload = new("finding_revised", ev.Revision, ev.RevisedAt,
                    JsonSerializer.SerializeToElement(ev, ComplianceCoreJsonContext.Default.FindingRevised));
                return true;
            case CorrectiveActionAdded ev when Scope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId) && ev.Action is not null:
                payload = new("corrective_action_added", ev.Revision, ev.Action.AddedAt,
                    JsonSerializer.SerializeToElement(ev, ComplianceCoreJsonContext.Default.CorrectiveActionAdded));
                return true;
            case CorrectiveActionCompleted ev when Scope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId):
                payload = new("corrective_action_completed", ev.Revision, ev.CompletedAt,
                    JsonSerializer.SerializeToElement(ev, ComplianceCoreJsonContext.Default.CorrectiveActionCompleted));
                return true;
            case FindingAcceptanceLinked ev when Scope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId) && ev.Acceptance is not null:
                payload = new("finding_acceptance_linked", ev.Revision, ev.Acceptance.LinkedAt,
                    JsonSerializer.SerializeToElement(ev, ComplianceCoreJsonContext.Default.FindingAcceptanceLinked));
                return true;
            case FindingClosed ev when Scope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId) && ev.Closure is not null:
                payload = new("finding_closed", ev.Revision, ev.Closure.ClosedAt,
                    JsonSerializer.SerializeToElement(ev, ComplianceCoreJsonContext.Default.FindingClosed));
                return true;
            case FindingReopened ev when Scope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId):
                payload = new("finding_reopened", ev.Revision, ev.ReopenedAt,
                    JsonSerializer.SerializeToElement(ev, ComplianceCoreJsonContext.Default.FindingReopened));
                return true;
            default:
                return false;
        }
    }

    public static DomainEvent Deserialize(string kind, JsonElement payload) => kind switch
    {
        "finding_raised" => payload.Deserialize(ComplianceCoreJsonContext.Default.FindingRaised)!,
        "finding_revised" => payload.Deserialize(ComplianceCoreJsonContext.Default.FindingRevised)!,
        "corrective_action_added" => payload.Deserialize(
            ComplianceCoreJsonContext.Default.CorrectiveActionAdded)!,
        "corrective_action_completed" => payload.Deserialize(
            ComplianceCoreJsonContext.Default.CorrectiveActionCompleted)!,
        "finding_acceptance_linked" => payload.Deserialize(
            ComplianceCoreJsonContext.Default.FindingAcceptanceLinked)!,
        "finding_closed" => payload.Deserialize(ComplianceCoreJsonContext.Default.FindingClosed)!,
        "finding_reopened" => payload.Deserialize(ComplianceCoreJsonContext.Default.FindingReopened)!,
        _ => throw new InvalidOperationException("The imported finding event kind is unknown."),
    };

    public static string Hash(JsonElement payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.GetRawText())));

    public static bool IsHash(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool Scope(Uuid eventTenantId, Uuid eventProgramId, Uuid eventFindingId,
        Uuid tenantId, Uuid programId, Uuid findingId) => eventTenantId == tenantId &&
        eventProgramId == programId && eventFindingId == findingId;

    static bool InProgram(Uuid eventTenantId, Uuid eventProgramId, Uuid tenantId,
        Uuid programId) => eventTenantId == tenantId && eventProgramId == programId;
}
