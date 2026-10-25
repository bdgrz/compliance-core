using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

public sealed class SystemDescriptionNativeVersionContractTests
{
    [Theory]
    [InlineData(typeof(SystemDescriptionApproved), "bdgrz.systemdescription.system-description-approved")]
    [InlineData(typeof(SystemDescriptionBound), "bdgrz.systemdescription.system-description-bound")]
    [InlineData(typeof(SystemDescriptionDraftCreated), "bdgrz.systemdescription.system-description-draft-created")]
    [InlineData(typeof(SystemDescriptionDraftRevised), "bdgrz.systemdescription.system-description-draft-revised")]
    [InlineData(typeof(SystemDescriptionNoChangeRecorded), "bdgrz.systemdescription.system-description-no-change-recorded")]
    [InlineData(typeof(SystemDescriptionReviewed), "bdgrz.systemdescription.system-description-reviewed")]
    [InlineData(typeof(SystemDescriptionSourcesBound), "bdgrz.systemdescription.system-description-sources-bound")]
    [InlineData(typeof(SystemDescriptionSuccessorProposed), "bdgrz.systemdescription.system-description-successor-proposed")]
    public void ShouldAdvanceOccupiedLegacySchemaGivenIncompatibleNativePayload(Type eventType, string name)
    {
        // Arrange
        var expected = name + "@2";

        // Act
        var actual = EventKind.Of(eventType);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ShouldPersistExactNativeEnvelopeAndRefuseLegacyInlineV1GivenDescriptionCreation()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        var rules = await fixture.SeedAsync();
        var created = Assert.IsType<SystemDescriptionDraftCreated>(Assert.Single(await fixture.FactsAsync()));
        await using var provider = EventPersistenceFrameTests.Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var nativeBytes = serializer.Serialize(created);
        var envelope = JsonNode.Parse(nativeBytes.Span)!.AsObject();
        var legacy = new JsonObject
        {
            ["name"] = "bdgrz.systemdescription.system-description-draft-created",
            ["version"] = 1,
            ["metadata"] = envelope["metadata"]!.DeepClone(),
            ["payload"] = new JsonObject
            {
                ["tenant_id"] = created.TenantId.ToString(),
                ["program_id"] = created.ProgramId.ToString(),
                ["system_description_id"] = created.SystemDescriptionId.ToString(),
                ["aggregate_revision"] = 1,
                ["version"] = JsonSerializer.SerializeToNode(rules.CurrentVersion!,
                    ComplianceCoreJsonContext.Default.SystemDescriptionVersionView),
                ["occurred_at"] = created.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
            },
        };
        var oldBytes = Encoding.UTF8.GetBytes(legacy.ToJsonString());

        // Act
        var failure = Record.Exception(() => serializer.Deserialize(oldBytes));
        var restored = Assert.IsType<SystemDescriptionDraftCreated>(serializer.Deserialize(nativeBytes));
        var roundTrip = serializer.Serialize(restored);

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(2, envelope["version"]!.GetValue<int>());
        Assert.Equal("bdgrz.systemdescription.system-description-draft-created", envelope["name"]!.GetValue<string>());
        Assert.Equal(created.Metadata, restored.Metadata);
        Assert.Equal(created.Operation, restored.Operation);
        Assert.Equal(created.VersionContent, restored.VersionContent);
        Assert.Equal(created.CurrentAuthorityContent, restored.CurrentAuthorityContent);
        Assert.Equal(created.FirstResponse, restored.FirstResponse);
        Assert.Equal(created.OperationResult, restored.OperationResult);
        Assert.Equal(nativeBytes.ToArray(), roundTrip.ToArray());
    }

    [Fact]
    public async Task ShouldVerifyNativeV2AndRejectLegacyKindGivenExactBrokerSourceProof()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync();
        var snapshot = await fixture.Descriptions.ReadAsync(fixture.Tenant, fixture.Program, fixture.Description);
        var created = Assert.IsType<SystemDescriptionDraftCreated>(Assert.Single(snapshot.Facts));
        ulong? offset = null;
        await foreach (var record in fixture.Events.ReadAsync(snapshot.Stream, 0, CancellationToken.None))
            if (record.Event.Metadata.EventId == created.Metadata.EventId)
                offset = record.ResourceOffset;
        var physical = offset ?? throw new InvalidOperationException("The actual native source record is not present.");
        var resolver = new EventStoreSourceFactResolver(fixture.Events);
        var source = await resolver.ResolveAsync(snapshot.Stream, physical);
        var locator = source.Locator;
        var required = await fixture.Content.WriteJsonAsync(fixture.Tenant.ToString(), Uuid.CreateVersion4(),
            SourceFactProofs.RequiredFactsSchema, locator);
        var proof = new SourceFactProof<SystemDescriptionDraftCreated>(locator.Source, locator.EventId,
            locator.ResourcePosition, locator.AggregateVersion, EventKind.Of(created), locator.PayloadSha256,
            required, locator.GlobalPosition, locator.RealmPosition);

        // Act
        var verified = await resolver.VerifyAsync(proof);
        var refusal = await Record.ExceptionAsync(() => resolver.VerifyAsync(proof with
        { EventKind = "bdgrz.systemdescription.system-description-draft-created@1" }));

        // Assert
        Assert.Equal("bdgrz.systemdescription.system-description-draft-created@2", proof.EventKind);
        Assert.Equal(1UL, proof.OwnerRevision);
        Assert.Equal(physical, proof.PhysicalPosition);
        Assert.Equal(created.Metadata.EventId, verified.EventId);
        Assert.Equal(created.VersionContent, verified.Facts.VersionContent);
        Assert.IsType<SourceFactVerificationException>(refusal);
    }
}
