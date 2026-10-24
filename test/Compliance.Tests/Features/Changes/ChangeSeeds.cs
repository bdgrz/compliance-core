using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Changes;

/// <summary>Publishes real verified native facts for rule-arranged fixture histories; never a production compatibility path.</summary>
static class ChangeSeeds
{
    public static IImmutableContentStore Content() => new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public static async ValueTask<SignificantChange> WriteAsync(IAggregateWriter writer, IImmutableContentStore content,
        SignificantChangeRules source)
    {
        var owner = await OwnerAsync(content, source).ConfigureAwait(false);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System)).ConfigureAwait(false);
        return owner;
    }
    public static async ValueTask<SignificantChange> OwnerAsync(IImmutableContentStore content, SignificantChangeRules source)
    {
        var owner = new SignificantChange(source.TenantId, source.ProgramId, source.Id);
        var rules = new SignificantChangeRules(source.TenantId, source.ProgramId, source.Id);
        foreach (var change in source.AppliedChanges)
        {
            var before = rules.Current;
            rules.ClearPendingChanges();
            rules.RaiseEvent(change);
            var state = rules.Current!;
            var kind = change switch
            {
                SignificantChangeRecordedBody => nameof(RecordSignificantChange),
                SignificantChangeRevisedBody => nameof(ReviseSignificantChange),
                SignificantChangeImpactAssessedBody => nameof(AssessSignificantChange),
                ImpactReviewWorkLinkedBody => nameof(LinkImpactReviewWork),
                SignificantChangeSystemDescriptionDecisionRecordedBody => nameof(RecordSignificantChangeSystemDescriptionDecision),
                _ => throw new InvalidOperationException("The fixture has another rule mutation."),
            };
            var memberCommand = kind is nameof(RecordSignificantChange) or nameof(ReviseSignificantChange) or nameof(AssessSignificantChange);
            var actor = memberCommand ? ActorReference.ForMember(state.ActorMemberId, state.ActorDisplay) : ActorReference.ForSystemProcess("fixture", "fixture");
            var id = before is null ? source.Id : Uuid.CreateVersion5(source.Id, $"seed:{owner.NativeSequence + 1}:{kind}");
            var op = OwnerProvenance.For(owner, id, kind, actor, source.Id.ToString(),
                before is not null && memberCommand ? unchecked((ulong)before.Revision) : null,
                AuthoredIntentDigest.Compute(state), state.UpdatedAt) with
            { FirstOwner = owner.Stream };
            var frozen = await SignificantChangeContentStore.FreezeAsync(content, before, rules, kind, CancellationToken.None).ConfigureAwait(false);
            var response = await NativeContent.FreezeUnitAsync(content, source.TenantId).ConfigureAwait(false);
            var first = await FirstResponses.FreezeAsync(content, source.TenantId.ToString(), response.Response).ConfigureAwait(false);
            var result = owner.Append(SignificantChangeContentStore.Fact(frozen, owner.NativeSequence + 1, op, first, response.Result),
                frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Error!.Message);
        }
        return owner;
    }
}
