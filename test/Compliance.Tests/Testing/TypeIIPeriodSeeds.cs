using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit fixture publication through compact native period facts and real immutable content.</summary>
static class TypeIIPeriodSeeds
{
    public static IImmutableContentStore NewContentStore() => new FitzKvImmutableContentStore(
        new Bdgrz.Compliance.Hosting.Persistence.PersistenceKvClient(new Cntryl.Fitz.Testing.InMemoryKvClient()));

    public static Task WriteAsync(IAggregateWriter writer, IImmutableContentStore content, TypeIIPeriodRules rules,
        CancellationToken ct = default) => SaveAsync(null, writer, content, rules, ct);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        TypeIIPeriodRules rules, CancellationToken ct = default) => SaveAsync(reader, writer, content, rules, ct);

    static async Task SaveAsync(IAggregateReader? reader, IAggregateWriter writer, IImmutableContentStore content,
        TypeIIPeriodRules source, CancellationToken ct)
    {
        var changes = source.PendingChanges.ToArray();
        var original = source.AppliedChanges.Take(source.AppliedChanges.Count - changes.Length).ToArray();
        var current = new TypeIIPeriodRules(source.TenantId, source.ProgramId, source.Id);
        foreach (var change in original)
            current.Apply(change);
        current.ClearPendingChanges();
        var owner = new TypeIIPeriod(source.TenantId, source.ProgramId, source.Id);
        if (reader is not null)
            owner = await reader.HydrateAsync(owner, ct);
        if (owner.CurrentContent is { } prior && (current.Current is null || ContentDigest.OfJson(current.Current) != prior.Sha256) ||
            owner.CurrentContent is null && current.Current is not null)
            throw new InvalidOperationException("The period fixture's transient rules differ from the native source cut.");
        for (var index = 0; index < changes.Length; index++)
        {
            var change = changes[index];
            current.ClearPendingChanges();
            if (change is TypeIIPeriodOpeningAccepted opening)
            {
                var body = changes.Skip(index + 1).OfType<ObservationPeriodStartedBody>().First();
                var operation = opening.Operation with { CommandKind = nameof(StartTypeIIObservationPeriod) };
                var changeRef = await content.WriteJsonAsync(owner.Stream.Realm, Uuid.CreateVersion4(), TypeIIPeriodOpeningChange.Schema,
                    new TypeIIPeriodOpeningChange(body.State), ct);
                var intent = await IntentAsync(content, owner, operation, TypeIIRangeOperationKind.Opening, null,
                    new ObservationRange(body.State.PeriodStart, body.State.PeriodEnd), changeRef, ct);
                var response = await TypeIIRangeContent.AcceptedResponseAsync(content, owner.Stream.Realm, source.Id, operation.AcceptedAt, ct);
                var first = await FirstResponses.FreezeAsync(content, owner.Stream.Realm, response, ct);
                var raised = owner.AcceptOpening(operation, intent, first);
                Assert.True(raised.IsSuccess, raised.Error?.Message);
                current.Apply(new TypeIIPeriodOpeningAccepted(owner.Stream, owner.Revision, TypeIIRangeEffects.Generation,
                    intent, null, operation, first, null, source.ProgramId));
            }
            else if (change is TypeIIPeriodAmendmentAccepted amendment)
            {
                var body = changes.Skip(index + 1).OfType<ObservationPeriodAmendedBody>().First();
                var delta = body.State.Amendments[^1];
                var operation = amendment.Operation with { CommandKind = nameof(AmendTypeIIObservationPeriod), OriginalExpectedRevision = (ulong)owner.Revision };
                var changeRef = await content.WriteJsonAsync(owner.Stream.Realm, Uuid.CreateVersion4(), TypeIIPeriodAmendmentChange.Schema,
                    new TypeIIPeriodAmendmentChange(delta.PeriodStart, delta.PeriodEnd, delta.Reason, delta.ActorMemberId,
                        delta.ActorDisplay, delta.AmendedAt), ct);
                var intent = await IntentAsync(content, owner, operation, TypeIIRangeOperationKind.Amendment,
                    new ObservationRange(current.Current!.PeriodStart, current.Current.PeriodEnd),
                    new ObservationRange(body.State.PeriodStart, body.State.PeriodEnd), changeRef, ct);
                var response = await TypeIIRangeContent.AcceptedResponseAsync(content, owner.Stream.Realm, operation.RootOperationId, operation.AcceptedAt, ct);
                var first = await FirstResponses.FreezeAsync(content, owner.Stream.Realm, response, ct);
                var raised = owner.AcceptAmendment(owner.Revision, operation, intent, first);
                Assert.True(raised.IsSuccess, raised.Error?.Message);
                current.Apply(new TypeIIPeriodAmendmentAccepted(owner.Stream, owner.Revision, TypeIIRangeEffects.Generation,
                    intent, null, operation, first, null, source.ProgramId));
            }
            else
            {
                var before = current.Current;
                change = change switch
                {
                    ObservationPeriodStartedBody started => started with { Change = TypeIIPeriodRules.IntentOf(current.Pending)! },
                    ObservationPeriodAmendedBody amended => amended with { Change = TypeIIPeriodRules.IntentOf(current.Pending)! },
                    _ => change,
                };
                var operation = Operation(change, owner);
                if (change is ObservationPeriodCloseProposedBody validation)
                {
                    var value = validation.State.CloseValidation!;
                    var reconciliation = new TypeIIPeriodCloseValidationContent(value.SourceReferences, value.ExpectedOccurrenceIds,
                        value.ActualOccurrenceIds, value.DuplicateOccurrenceIds, value.MissingWorkIds, value.UnresolvedMaterialItemIds)
                    {
                        RecordedThrough = value.RecordedThrough,
                        SourceContentManifest = value.SourceContentManifest,
                        SourceBlockers = value.SourceBlockers,
                        SourceDifferences = value.SourceDifferences,
                    };
                    var reference = await content.WriteJsonAsync(owner.Stream.Realm, Uuid.CreateVersion4(), TypeIIPeriodCloseValidationChange.Schema,
                        new TypeIIPeriodCloseValidationChange(Header(operation), reconciliation), ct);
                    change = validation with { Change = reference };
                }
                current.RaiseEvent(change);
                var frozen = await TypeIIPeriodContent.FreezeAsync(content, before, current, operation, ct);
                var response = await NativeContent.FreezeUnitAsync(content, owner.TenantId, ct);
                var first = change is ObservationPeriodStartedBody or ObservationPeriodAmendedBody ? null :
                    await FirstResponses.FreezeAsync(content, owner.Stream.Realm, response.Response, ct);
                var fact = TypeIIPeriodContent.Fact(frozen, operation, first, response.Result, owner.NativeSequence + 1, owner.CalendarIntent);
                var raised = owner.Append(fact, before, current.Current!, owner.NativeSequence, owner.CommittedStreamPosition);
                Assert.True(raised.IsSuccess, raised.Error?.Message);
            }
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
        if (source.Current is { } expected && (current.Current is null || ContentDigest.OfJson(expected) != ContentDigest.OfJson(current.Current)))
            throw new InvalidOperationException("The period fixture did not preserve all prepared source content.");
        source.ClearPendingChanges();
        source.SourcePhysicalPosition = owner.CommittedStreamPosition;
    }

    static async Task<ImmutableContentRef> IntentAsync(IImmutableContentStore content, TypeIIPeriod owner,
        OperationProvenance operation, TypeIIRangeOperationKind kind, ObservationRange? oldRange, ObservationRange newRange,
        ImmutableContentRef change, CancellationToken ct)
    {
        var calendar = new TypeIIObservationCalendar(owner.TenantId, owner.ProgramId).Stream;
        // This fixture does not issue a calendar permit. Production range-process fixtures use real admitted grants.
        var prior = oldRange is null ? null : new ReservationGrant(
            TypeIIRangeEffects.KeyOf(owner.CalendarAuthorization!.AuthorizationId, owner.Stream, calendar),
            (ulong)owner.CalendarAuthorization.Generation, new NativeFactReference(calendar, Uuid.CreateVersion4(), 1));
        return await content.WriteJsonAsync(owner.Stream.Realm, Uuid.CreateVersion4(), TypeIIRangeIntent.Schema,
            new TypeIIRangeIntent(Header(operation), owner.ProgramId, owner.Id, kind, prior, oldRange, newRange,
                change, TypeIIRangeEffects.KeyOf(operation.RootOperationId, owner.Stream, calendar)), ct);
    }

    static DurableIntentHeader Header(OperationProvenance operation) => new(operation.RootOperationId, operation.Owner,
        operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
        operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt);

    static OperationProvenance Operation(object value, TypeIIPeriod owner)
    {
        if (value is ObservationPeriodStartedBody started)
            return started.Operation;
        if (value is ObservationPeriodAmendedBody amended)
            return amended.Operation;
        if (value is DueControlOccurrencesGenerated generation)
            return generation.Operation;
        if (value is ObservationPeriodCloseProposedBody validation)
            return validation.Operation;
        var (kind, state, at) = value switch
        {
            TypeIIPeriodCloseApprovedBody approved => (nameof(ApproveTypeIIPeriodClose), approved.State, approved.OccurredAt),
            ObservationPeriodCloseFrozenBody frozen => (nameof(FreezeTypeIIPeriodClose), frozen.State, frozen.OccurredAt),
            TypeIIPeriodCloseAmendedBody close => (nameof(AmendTypeIIPeriodClose), close.State, close.OccurredAt),
            _ => throw new InvalidOperationException("The period fixture has another rule mutation kind."),
        };
        return RootOperation.Provenance(Uuid.CreateVersion5(owner.Id, "seed:" + kind + ":" + state.Revision), kind, owner.Stream,
            ActorReference.ForMember(state.ActorMemberId, state.ActorDisplay), owner.Revision, new string('e', 64), at);
    }
}
