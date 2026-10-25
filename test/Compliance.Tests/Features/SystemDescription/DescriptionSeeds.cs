using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using DescriptionOwner = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

/// <summary>Explicit native source fixture publication; never a production legacy adapter or permit.</summary>
static class DescriptionSeeds
{
    public static IImmutableContentStore Content() => new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public static async ValueTask<DescriptionOwner> WriteAsync(IAggregateWriter writer, IImmutableContentStore content, SystemDescriptionRules source)
    {
        var owner = new DescriptionOwner(source.TenantId, source.ProgramId, source.Id);
        var rules = new SystemDescriptionRules(source.TenantId, source.ProgramId, source.Id);
        await ApplyAsync(owner, rules, source.AppliedChanges, content).ConfigureAwait(false);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System)).ConfigureAwait(false);
        source.ClearPendingChanges();
        source.SourceNativeSequence = owner.NativeSequence;
        source.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return owner;
    }
    public static async ValueTask<DescriptionOwner> AppendAsync(IAggregateWriter writer, IAggregateReader reader, IDomainEventReader events,
        IImmutableContentStore content, SystemDescriptionRules source)
    {
        var snapshot = await new SystemDescriptionReader(reader, events, content).ReadAsync(source.TenantId, source.ProgramId, source.Id).ConfigureAwait(false);
        await ApplyAsync(snapshot.Owner, snapshot.Rules, source.PendingChanges.ToArray(), content).ConfigureAwait(false);
        await writer.SaveAsync(snapshot.Owner, new RequestDispatchContext(RequestActor.System)).ConfigureAwait(false);
        source.ClearPendingChanges();
        source.SourceNativeSequence = snapshot.Owner.NativeSequence;
        source.SourcePhysicalPosition = snapshot.Owner.CommittedStreamPosition;
        return snapshot.Owner;
    }
    public static async ValueTask<DescriptionOwner> OwnerAsync(IImmutableContentStore content, SystemDescriptionRules source)
    {
        var owner = new DescriptionOwner(source.TenantId, source.ProgramId, source.Id);
        await ApplyAsync(owner, new SystemDescriptionRules(source.TenantId, source.ProgramId, source.Id), source.AppliedChanges, content).ConfigureAwait(false);
        return owner;
    }
    static async ValueTask ApplyAsync(DescriptionOwner owner, SystemDescriptionRules rules, IReadOnlyList<object> changes, IImmutableContentStore content)
    {
        foreach (var value in changes)
        {
            var before = rules.Read();
            rules.ClearPendingChanges();
            rules.RaiseEvent(value);
            var kind = value switch
            {
                SystemDescriptionDraftCreatedBody => nameof(CreateSystemDescriptionDraft),
                SystemDescriptionDraftRevisedBody => nameof(ReviseSystemDescriptionDraft),
                SystemDescriptionSourcesBoundBody => nameof(BindSystemDescriptionSources),
                SystemDescriptionSuccessorProposedBody => nameof(ProposeSystemDescriptionSuccessor),
                SystemDescriptionReviewedBody => nameof(ReviewSystemDescription),
                SystemDescriptionApprovedBody => nameof(ApproveSystemDescription),
                SystemDescriptionNoChangeRecordedBody => nameof(RecordSystemDescriptionNoChangeDecision),
                SystemDescriptionBoundBody => nameof(BindSystemDescriptionToExamination),
                _ => throw new InvalidOperationException("The description fixture has another mutation kind."),
            };
            var id = before.Revision == 0 ? owner.Id : Uuid.CreateVersion5(owner.Id, $"seed:{owner.NativeSequence + 1}:{kind}");
            var member = SystemDescriptionContent.Actor(value);
            var op = OwnerProvenance.For(owner, id, kind, ActorReference.ForMember(member, "Fixture actor"), owner.Id.ToString(),
                before.Revision == 0 ? null : unchecked((ulong)before.Revision), AuthoredIntentDigest.Compute(rules.Read()), SystemDescriptionContent.At(value)) with
            { FirstOwner = owner.Stream };
            var frozen = await SystemDescriptionContent.FreezeAsync(content, before, rules, kind, null, CancellationToken.None).ConfigureAwait(false);
            frozen = frozen with { SourceSnapshotContent = owner.SourceSnapshotContent };
            var response = await NativeContent.FreezeUnitAsync(content, owner.TenantId).ConfigureAwait(false);
            var first = await FirstResponses.FreezeAsync(content, owner.TenantId.ToString(), response.Response).ConfigureAwait(false);
            var result = owner.Append(SystemDescriptionFactFactory.Create(frozen, owner.NativeSequence + 1, op, first, response.Result),
                frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
    }
}
