using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;

/// <summary>One tenant's wording. A selected command is retained for retry without accumulating receipt state.</summary>
public sealed class AcknowledgementWording : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _selectedCommandId;
    AcknowledgementWordingSet? _current;
    AcknowledgementWordingSet? _selected;

    public AcknowledgementWording(Uuid tenantId, Uuid selectedCommandId = default)
        : base(tenantId, new EventStreamAddress(tenantId.ToString(), "acknowledgement-wording", tenantId.ToString()))
    {
        _tenantId = tenantId;
        _selectedCommandId = selectedCommandId;
        On<AcknowledgementWordingSet>(Apply);
    }

    public long Revision => _current?.Revision ?? 0;
    public string Wording => _current?.Wording ?? PolicyDistributionCampaign.DefaultAcknowledgementText;
    public AcknowledgementWordingView View() => new(_tenantId, Revision, Wording, _current?.SetBy, _current?.SetAt);

    public CommandFailure? Set(SetAcknowledgementWording request, ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        if (_selected is { } accepted && accepted.CommandId == request.CommandId)
            return request.TenantId == _tenantId && request.ExpectedRevision == accepted.OriginalExpectedRevision &&
                AuthoredIntentDigest.Compute(request) == accepted.AuthoredIntentSha256 && actor.Kind == accepted.SetBy.Kind &&
                actor.Id == accepted.SetBy.Id ? null : CommandFailure.StateConflict(
                    "The wording command already accepted another actor or authored intent.");
        if (_tenantId == Uuid.Empty || request.TenantId != _tenantId || request.CommandId == Uuid.Empty ||
            request.ExpectedRevision < 0 || string.IsNullOrWhiteSpace(request.Wording) || request.Wording.Length > 4000 ||
            actor.Kind != "member" || !Uuid.TryParse(actor.Id, out var memberId) || memberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(actor.Display) || at == default)
            return CommandFailure.InvalidContent("Wording requires its tenant, command, member and 1 to 4000 characters.");
        if (request.ExpectedRevision != Revision)
            return CommandFailure.StateConflict("The organization acknowledgement wording changed. Reload it and retry.");
        if (request.Wording == Wording)
            return null;
        RaiseEvent(new AcknowledgementWordingSet(_tenantId, Revision + 1, request.CommandId,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), request.Wording, actor, at));
        return null;
    }

    void Apply(AcknowledgementWordingSet fact)
    {
        if (fact.TenantId != _tenantId || fact.Revision != Revision + 1 ||
            fact.OriginalExpectedRevision != Revision || fact.CommandId == Uuid.Empty ||
            fact.AuthoredIntentSha256 is not { Length: 64 } || !fact.AuthoredIntentSha256.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(fact.Wording) || fact.Wording.Length > 4000 || fact.Wording == Wording ||
            fact.SetBy.Kind != "member" || !Uuid.TryParse(fact.SetBy.Id, out var memberId) || memberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(fact.SetBy.Display) || fact.SetAt == default ||
            _current is { } prior && fact.SetAt < prior.SetAt)
            throw new InvalidOperationException("The acknowledgement wording history is invalid.");
        _current = fact;
        if (fact.CommandId == _selectedCommandId)
        {
            if (_selected is not null)
                throw new InvalidOperationException("A wording command was accepted more than once.");
            _selected = fact;
        }
    }
}
