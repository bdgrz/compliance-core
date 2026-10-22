using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Adapts native schemas to the unchanged business reducers without changing stored payloads.</summary>
static class FindingEventFacts
{
    public static Uuid? RequestId(DomainEvent ev) => ev switch
    {
        FindingRaisedV2 fact => fact.RequestId,
        FindingRevisedV2 fact => fact.RequestId,
        CorrectiveActionAddedV2 fact => fact.RequestId,
        CorrectiveActionCompletedV2 fact => fact.RequestId,
        FindingAcceptanceLinkedV2 fact => fact.RequestId,
        FindingClosedV2 fact => fact.RequestId,
        FindingReopenedV2 fact => fact.RequestId,
        _ => null,
    };

    public static DomainEvent Business(DomainEvent ev)
    {
        var fact = ev switch
        {
            FindingRaisedV2 x => new FindingRaised(x.TenantId, x.ProgramId, x.FindingId, x.Revision,
                x.Source, x.Title, x.Description, x.Severity, x.AffectedScope, x.OwnerMemberId,
                x.DueOn, Array.AsReadOnly(x.Links.ToArray()), x.RaisedBy, x.RaisedAt),
            FindingRevisedV2 x => new FindingRevised(x.TenantId, x.ProgramId, x.FindingId, x.Revision,
                x.Severity, x.OwnerMemberId, x.DueOn, x.AffectedScope, x.RootCause, x.Reason, x.RevisedBy, x.RevisedAt),
            CorrectiveActionAddedV2 x => new CorrectiveActionAdded(x.TenantId, x.ProgramId, x.FindingId, x.Revision, x.Action),
            CorrectiveActionCompletedV2 x => new CorrectiveActionCompleted(x.TenantId, x.ProgramId, x.FindingId, x.Revision,
                x.ActionId, x.ResolutionNotes, Array.AsReadOnly(x.Evidence.ToArray()), x.CompletedByMemberId, x.CompletedBy, x.CompletedAt),
            FindingAcceptanceLinkedV2 x => new FindingAcceptanceLinked(x.TenantId, x.ProgramId, x.FindingId, x.Revision, x.Acceptance),
            FindingClosedV2 x => new FindingClosed(x.TenantId, x.ProgramId, x.FindingId, x.Revision, x.Closure),
            FindingReopenedV2 x => new FindingReopened(x.TenantId, x.ProgramId, x.FindingId, x.Revision, x.Reason, x.ReopenedBy, x.ReopenedAt),
            _ => ev,
        };
        if (!ReferenceEquals(ev, fact))
            fact.AttachMetadata(ev.Metadata);
        return fact;
    }

    public static JsonElement Serialize(DomainEvent ev) => JsonSerializer.SerializeToElement(ev,
        ev.GetType(), ComplianceCoreJsonContext.Default);

    public static string Hash(DomainEvent ev) => FindingHistoryEventPayloads.Hash(Serialize(ev));

    public static DomainEvent Freeze(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        Serialize(ev), ev.GetType(), ComplianceCoreJsonContext.Default)!;

    public static bool IsInitialActionFor(FindingRaisedV2 raised, DomainEvent ev) =>
        ev is CorrectiveActionAddedV2 added && raised.CreationKind == "routed" &&
        raised.RequestId == added.RequestId && raised.TenantId == added.TenantId &&
        raised.ProgramId == added.ProgramId && raised.FindingId == added.FindingId &&
        added.Revision == 2 && raised.InitialActionSha256 == Hash(added) &&
        raised.RaisedBy.Kind == added.Action.AddedBy.Kind && raised.RaisedBy.Id == added.Action.AddedBy.Id &&
        raised.RaisedAt == added.Action.AddedAt &&
        added.Action.ActionId == Uuid.CreateVersion5(raised.FindingId, "corrective-action");
}
