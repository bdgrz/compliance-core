using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessExpectationReplayCutTests
{
    [Fact]
    public async Task ShouldRejectUnselectedIntermediateMetadataSubstitutionGivenActualHydratedLineCut()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var parameters = new AccessExpectationParameters(PrincipalKind: "access_token");
        var first = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind,
            parameters, revision: first.Revision, supersedes: first.ExpectationId,
            effectiveFrom: AccessReviewFixture.Observed));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IAggregateReader>().HydrateAsync(
            new AccessExpectation(fixture.TenantId, first.ExpectationId));
        var facts = new List<DomainEvent>();
        await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0,
                           CancellationToken.None))
            facts.Add(record.Event);
        var original = Assert.IsType<AccessExpectationApproved>(facts[1]);
        var changed = original with { };
        changed.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4() });
        facts[1] = changed;

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await AccessExpectationHistory.ReadAsync(
            new Records(owner.Stream, facts.ToArray()), owner));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3, owner.Revision);
        Assert.IsType<AccessExpectationProposed>(owner.Latest);
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
