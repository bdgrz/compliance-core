using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Queries;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestPreferenceNativeTests
{
    [Theory]
    [InlineData(false, "America/New_York")]
    [InlineData(true, "UTC")]
    public async Task ShouldReplayOriginalPreferenceWithoutAppendGivenLaterPreferenceChange(bool enabled, string zone)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = Request(Uuid.CreateVersion4(), fixture.TenantId, enabled, zone);
        var original = await fixture.AsAsync(fixture.OwnerUserId, request);
        await fixture.AsAsync(fixture.OwnerUserId, Request(Uuid.CreateVersion4(), fixture.TenantId, !enabled, "Europe/London"));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var before = await reader.HydrateAsync(new WorkDigestPreference(fixture.TenantId, fixture.OwnerMemberId));

        // Act
        var replay = await fixture.AsAsync(fixture.OwnerUserId, request);
        var after = await reader.HydrateAsync(new WorkDigestPreference(fixture.TenantId, fixture.OwnerMemberId));

        // Assert
        Assert.Equal(original, replay);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(before.Read(), after.Read());
    }

    [Fact]
    public async Task ShouldRefuseChangedAuthoredPreferenceGivenAcceptedCommandIdentity()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var id = Uuid.CreateVersion4();
        await fixture.AsAsync(fixture.OwnerUserId, Request(id, fixture.TenantId, false, "UTC"));
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var changed = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            Request(id, fixture.TenantId, true, "UTC"), ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.False((await fixture.AsAsync(fixture.OwnerUserId, new GetWorkDigestPreference(fixture.TenantId))).EmailDigestEnabled);
    }

    [Fact]
    public async Task ShouldRefuseOwnOriginalReceiptGivenCanonicalMembershipSuspended()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = Request(Uuid.CreateVersion4(), fixture.TenantId, false, "UTC");
        await fixture.AsAsync(fixture.OwnerUserId, request);
        await fixture.SuspendAsync(fixture.OwnerUserId);
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var refused = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request,
            ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, refused.Error?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldClassifyUnavailableOrCorruptOriginalReceiptGivenAcceptedPreference(bool corrupt)
    {
        // Arrange
        ReceiptFault content = null!;
        var fixture = await OperationsFixture.CreateAsync(configureContent: inner => content = new ReceiptFault(inner));
        var request = Request(Uuid.CreateVersion4(), fixture.TenantId, false, "UTC");
        await fixture.AsAsync(fixture.OwnerUserId, request);
        content.Fail = true;
        content.Corrupt = corrupt;
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var refused = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request,
            ProgramManagementServices.Actor(fixture.OwnerUserId));
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new WorkDigestPreference(fixture.TenantId, fixture.OwnerMemberId));

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Equal(!corrupt, refused.Error?.IsTransient);
        Assert.Equal(1UL, current.CommittedStreamPosition);
        Assert.False(current.Read().EmailDigestEnabled);
    }

    sealed class ReceiptFault(IImmutableContentStore inner) : IImmutableContentStore
    {
        public bool Fail { get; set; }
        public bool Corrupt { get; set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (Fail && reference.Schema == FirstResponses.Schema)
                throw Corrupt ? new ImmutableContentIntegrityException("Corrupt original preference descriptor.")
                    : new ImmutableContentUnavailableException("Missing original preference descriptor.");
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    static SetWorkDigestPreference Request(Uuid id, Uuid tenant, bool enabled, string zone)
    {
        var json = $$"""{"command_id":"{{id}}","tenant_id":"{{tenant}}","email_digest_enabled":{{enabled.ToString().ToLowerInvariant()}},"time_zone_id":"{{zone}}"}""";
        return JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.SetWorkDigestPreference)!;
    }
}
