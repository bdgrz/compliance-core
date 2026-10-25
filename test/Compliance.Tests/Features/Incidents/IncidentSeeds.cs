using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Incidents;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Incidents;

/// <summary>Publishes pure fixture rules through the same compact native append and immutable proof seams as production.</summary>
static class IncidentSeeds
{
    public static async ValueTask<ComplianceIncident> WriteAsync(IAggregateWriter writer, IImmutableContentStore content, ComplianceIncidentRules source)
    {
        var owner = await OwnerAsync(content, source);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        source.ClearPendingChanges();
        return owner;
    }
    public static async ValueTask<ComplianceIncident> OwnerAsync(IImmutableContentStore content, ComplianceIncidentRules source)
    {
        var owner = new ComplianceIncident(source.TenantId, source.ProgramId, source.Id);
        var cursor = new ComplianceIncidentRules(source.TenantId, source.ProgramId, source.Id);
        await AppendAsync(owner, cursor, null, source.AppliedChanges, content);
        return owner;
    }
    public static async ValueTask<ComplianceIncident> AppendAsync(IAggregateWriter writer, ComplianceIncidentReader reader, IImmutableContentStore content, ComplianceIncidentRules source)
    {
        var snapshot = await reader.ReadAsync(source.TenantId, source.ProgramId, source.Id);
        await AppendAsync(snapshot.Owner, snapshot.Rules, snapshot.Authority, source.PendingChanges.ToArray(), content);
        await writer.SaveAsync(snapshot.Owner, new RequestDispatchContext(RequestActor.System));
        source.ClearPendingChanges();
        return snapshot.Owner;
    }
    static async ValueTask AppendAsync(ComplianceIncident owner, ComplianceIncidentRules cursor, ComplianceIncidentCurrentAuthority? prior,
        IReadOnlyList<object> changes, IImmutableContentStore content)
    {
        foreach (var value in changes)
        {
            var before = cursor.Current;
            cursor.ClearPendingChanges();
            cursor.RaiseEvent(value);
            var state = cursor.Current!;
            var kind = value switch
            {
                ComplianceIncidentRecordedBody => nameof(RecordComplianceIncident),
                ComplianceIncidentRevisedBody => nameof(ReviseComplianceIncident),
                ComplianceIncidentImpactAssessedBody => nameof(AssessComplianceIncident),
                ComplianceIncidentSystemDescriptionDecisionRecordedBody => nameof(RecordComplianceIncidentSystemDescriptionDecision),
                IncidentResolutionRecordedBody => nameof(RecordComplianceIncidentResolution),
                _ => throw new InvalidOperationException("Another incident fixture mutation kind."),
            };
            var command = before is null ? owner.Id : Uuid.CreateVersion5(owner.Id, $"seed:{owner.NativeSequence + 1}:{kind}");
            var actor = kind == nameof(RecordComplianceIncidentSystemDescriptionDecision)
                ? ActorReference.ForSystemProcess("system", "system") : ActorReference.ForMember(state.ActorMemberId, state.ActorDisplay);
            var op = OwnerProvenance.For(owner, command, kind, actor, owner.Id.ToString(), before is null || kind == nameof(RecordComplianceIncidentSystemDescriptionDecision)
                ? null : unchecked((ulong)before.Revision), AuthoredIntentDigest.Compute(state), state.UpdatedAt) with
            { FirstOwner = owner.Stream };
            var frozen = await ComplianceIncidentContentStore.FreezeAsync(content, before, prior, owner.Header, cursor, kind, CancellationToken.None);
            var response = await NativeContent.FreezeUnitAsync(content, owner.TenantId);
            var first = await FirstResponses.FreezeAsync(content, owner.TenantId.ToString(), response.Response);
            var result = owner.Append(ComplianceIncidentContentStore.Fact(frozen, owner.NativeSequence + 1, op, first, response.Result), frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(result.IsSuccess, result.Error?.Message);
            prior = frozen.Authority;
        }
    }
}
