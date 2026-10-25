using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Risks;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskMethodPrepareFailureTests
{
    [Fact]
    public async Task ShouldReplayEqualWinnerGivenPreparedContentFailureAfterConcurrentAdmission()
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        var request = fixture.Request(0, 12);
        RiskMethodVersionView? winner = null;
        var content = new ConcurrentAdmissionContent(fixture.Content, async () =>
        {
            var accepted = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            winner = accepted.Value;
            fixture.Clock.Now = fixture.Clock.Now.AddDays(2);
        });
        var handler = new PublishRiskMethodVersionHandler(new NativeOperationExecutor(fixture.Reader, fixture.Executor,
            content), fixture.Reader, content, fixture.Clock);

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(winner);
        Assert.Equal(winner.MethodVersionId, result.Value!.MethodVersionId);
        Assert.Equal(winner.PublishedAt, result.Value.PublishedAt);
        Assert.Equal(1, (await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program))).PublishedVersion);
    }

    sealed class ConcurrentAdmissionContent(IImmutableContentStore inner, Func<Task> admit) : IImmutableContentStore
    {
        bool _armed = true;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Cntryl.Portia.Uuid id, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (_armed && schema == RiskMethodContent.Schema)
            {
                _armed = false;
                await admit();
                throw new ImmutableContentUnavailableException("The prepared write lost its response after another equal request committed.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
