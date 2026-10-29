using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class TypeIICalendarMalformedHistoryTests
{
    [Theory]
    [InlineData("regrant_after_fence")]
    [InlineData("duplicate_fence")]
    [InlineData("wrong_period_release")]
    [InlineData("second_activation")]
    public async Task ShouldRejectMalformedUnselectedHistoryGivenCurrentCalendarRead(string malformed)
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var forged = await ForgeAsync(fixture, malformed);
        TypeIICalendarContent.Validate(forged.Authority);
        var counterOnly = new AggregateScenario<TypeIIObservationCalendar>(
            new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId)).Given(forged.Facts.ToArray()).Aggregate;

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () =>
            await fixture.CalendarAsync());

        // Assert
        Assert.Contains("native transition", failure.Message, StringComparison.Ordinal);
        Assert.Equal(forged.Authority.Revision, counterOnly.Revision);
        Assert.Equal(forged.Authority.ReservationGeneration, counterOnly.ReservationGeneration);
        Assert.Equal(forged.Facts.Count, (await fixture.Host.EventsAsync<DomainEvent>(fixture.CalendarStream)).Count);
    }

    [Fact]
    public async Task ShouldRejectBeforeExecutorDelegateGivenMalformedUnselectedCalendarHistory()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var forged = await ForgeAsync(fixture, "duplicate_fence");
        var invoked = false;
        var selected = ((INativeOperationFact)forged.Facts[0]).Operation.LogicalId;

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () =>
            await fixture.Host.Executor.ExecuteAsync(new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId,
                selected), _ =>
            {
                invoked = true;
                return AggregateOutcome.Discard(Result.Success);
            }, new RequestDispatchContext(RequestActor.System)));

        // Assert
        Assert.False(invoked);
        Assert.Contains("native transition", failure.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await fixture.Host.EventsAsync<DomainEvent>(fixture.CalendarStream)).Count);
    }

    [Fact]
    public async Task ShouldRejectActualAcquisitionRetryGivenMalformedLaterUnselectedCalendarTransition()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var root = await fixture.StartPeriodAsync();
        var period = await fixture.PeriodAsync(root);
        var accepted = period.AcceptedFor(root)!;
        var intentReference = TypeIIPeriod.IntentOf(accepted)!;
        var intent = await fixture.Host.Content.ReadJsonAsync<TypeIIRangeIntent>(fixture.TenantId.ToString(), intentReference);
        var commandId = TypeIIRangeEffects.ReceiverCommandId(accepted.Metadata.EventId,
            nameof(AcquireTypeIIPeriodRange), intent.Header.Episode);
        var command = new AcquireTypeIIPeriodRange(commandId, NativeFactReference.Of(period.Stream, accepted),
            fixture.CalendarStream, root, TypeIIRangeEffects.Generation, null, intentReference, root, intent.OldRange, intent.NewRange);
        var calendar = await fixture.CalendarAsync(root);
        var operation = Operation(fixture, root, nameof(SettleTypeIIPeriodRange));
        var change = new TypeIICalendarSettlementChange(operation, intentReference, root,
            TypeIIRangeSettlementKind.Activated, root, null);
        var revision = calendar.Revision + 1;
        var live = calendar.Authority.Live.Single() with
        {
            SettlementRevision = revision,
            SettlementBindingSha256 = TypeIICalendarContent.Binding(change),
        };
        var authority = calendar.Authority with { Revision = revision, Live = [live] };
        await AppendRawAsync(fixture, calendar.Authority, authority, null, change);

        // Act
        var result = await fixture.Host.Services.GetRequiredService<TypeIIRangeReceivers>().AcquireAsync(
            new RequestDispatchContext(RequestActor.System), command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Equal(3, (await fixture.Host.EventsAsync<DomainEvent>(fixture.CalendarStream)).Count);
    }

    static async Task<Forged> ForgeAsync(TypeIIRangeFixture fixture, string malformed)
    {
        var root = Uuid.CreateVersion4();
        var period = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref(TypeIIRangeIntent.Schema);
        var range = new ObservationRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var authority = TypeIICalendarContent.Empty(fixture.TenantId, fixture.ProgramId);
        var facts = new List<DomainEvent>();
        async Task SaveAsync(object change, TypeIICalendarReservationContent? live,
            TypeIICalendarReservationContent? closed)
        {
            var before = authority;
            var revision = before.Revision + 1;
            TypeIICalendarHistoryNode? node = null;
            var head = before.ReleasedHistory;
            if (closed is not null)
            {
                node = new(fixture.TenantId, fixture.ProgramId, revision, before.TerminalCount + 1, head, [closed]);
                head = await NativeContent.WriteAsync(fixture.Host.Content, fixture.TenantId,
                    TypeIICalendarContent.HistorySchema, node);
            }
            authority = new(fixture.TenantId, fixture.ProgramId, revision,
                before.ReservationGeneration + (change is TypeIICalendarAuthorizationChange ? 1 : 0),
                before.TerminalCount + (closed is null ? 0 : 1), head, live is null ? [] : [live]);
            facts.Add(await AppendRawAsync(fixture, before, authority, node, change));
        }
        TypeIICalendarSettlementChange Fence() => new(Operation(fixture, root, nameof(SettleTypeIIPeriodRange)),
            intent, period, TypeIIRangeSettlementKind.Fenced, root, null);
        TypeIICalendarReservationContent ClosedFence(TypeIICalendarSettlementChange value, long revision) => new(root,
            period, TypeIIRangeReservationState.Fenced, TypeIIRangeOperationKind.Opening, null, 0, null, null, revision)
        { SettlementBindingSha256 = TypeIICalendarContent.Binding(value) };
        var authorization = new TypeIICalendarAuthorizationChange(Operation(fixture, root, nameof(AcquireTypeIIPeriodRange)),
            intent, period, TypeIIRangeOperationKind.Opening, range, null);
        if (malformed is "regrant_after_fence" or "duplicate_fence")
        {
            var fence = Fence();
            await SaveAsync(fence, null, ClosedFence(fence, 1));
            if (malformed == "duplicate_fence")
            {
                var duplicate = Fence();
                await SaveAsync(duplicate, null, ClosedFence(duplicate, 2));
                return new(authority, facts);
            }
        }
        var pending = new TypeIICalendarReservationContent(root, period, TypeIIRangeReservationState.Pending,
            TypeIIRangeOperationKind.Opening, range, 1, null, authority.Revision + 1, null)
        { AuthorizationBindingSha256 = TypeIICalendarContent.Binding(authorization) };
        await SaveAsync(authorization, pending, null);
        if (malformed == "regrant_after_fence")
            return new(authority, facts);
        var activation = new TypeIICalendarSettlementChange(Operation(fixture, root, nameof(SettleTypeIIPeriodRange)),
            intent, period, TypeIIRangeSettlementKind.Activated, root, null);
        var active = pending with
        {
            State = TypeIIRangeReservationState.Active,
            SettlementRevision = authority.Revision + 1,
            SettlementBindingSha256 = TypeIICalendarContent.Binding(activation)
        };
        await SaveAsync(activation, active, null);
        if (malformed == "second_activation")
        {
            var duplicate = activation with { Operation = Operation(fixture, root, nameof(SettleTypeIIPeriodRange)) };
            await SaveAsync(duplicate, active with
            {
                SettlementRevision = authority.Revision + 1,
                SettlementBindingSha256 = TypeIICalendarContent.Binding(duplicate)
            }, null);
        }
        else
        {
            var release = new TypeIICalendarSettlementChange(Operation(fixture, root, nameof(SettleTypeIIPeriodRange)),
                intent, Uuid.CreateVersion4(), TypeIIRangeSettlementKind.Released, root, null);
            await SaveAsync(release, null, active with
            {
                State = TypeIIRangeReservationState.Released,
                SettlementRevision = authority.Revision + 1,
                SettlementBindingSha256 = TypeIICalendarContent.Binding(release)
            });
        }
        return new(authority, facts);
    }

    static async Task<DomainEvent> AppendRawAsync(TypeIIRangeFixture fixture, TypeIICalendarAuthority before,
        TypeIICalendarAuthority after, TypeIICalendarHistoryNode? node, object change)
    {
        var reference = await NativeContent.WriteAsync(fixture.Host.Content, fixture.TenantId,
            TypeIICalendarContent.AuthoritySchema, after);
        var prepared = new TypeIICalendarPrepared(before, after, reference, [], node, change);
        var fact = TypeIICalendarContent.Fact(prepared);
        DomainEventSeed.Attach(fact, fixture.ProgramId, (ulong)after.Revision);
        await fixture.Host.Services.GetRequiredService<IEventStore>().AppendAsync(fixture.CalendarStream,
            (ulong)before.Revision, [fact], CancellationToken.None);
        return fact;
    }

    static OperationProvenance Operation(TypeIIRangeFixture fixture, Uuid root, string commandKind) => new(
        Uuid.CreateVersion4(), root, null, commandKind, ActorReference.ForMember(fixture.ActorMemberId, "Calendar test"),
        fixture.CalendarStream, "initial", null, new string('a', 64), TypeIIRangeFixture.StartedAt,
        fixture.CalendarStream, fixture.CalendarStream, new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, root).Stream);
    sealed record Forged(TypeIICalendarAuthority Authority, IReadOnlyList<DomainEvent> Facts);
}
