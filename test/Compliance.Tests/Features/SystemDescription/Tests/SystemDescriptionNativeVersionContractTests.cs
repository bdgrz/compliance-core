using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Events;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;

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
