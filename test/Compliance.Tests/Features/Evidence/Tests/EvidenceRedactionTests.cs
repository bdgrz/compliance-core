using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using static Bdgrz.Compliance.Tests.Features.Evidence.Tests.EvidenceRedactionCompositionTests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceRedactionTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Contributor = ActorReference.ForMember(Uuid.CreateVersion4(), "Contributor");

    [Fact]
    public async Task ShouldRetainExactOriginalAndDistinctDerivativeGivenAttributedPreparation()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manually prepared replacement", "Remove private employee fields");

        // Act
        var result = await DispatchAsync(fixture, fixture.UserId, request);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.ArtifactId, history.CurrentPreparation!.Original.ArtifactId);
        Assert.Equal(derived, history.CurrentPreparation.Derived.ArtifactId);
        Assert.Equal(request.CommandId, history.CurrentPreparation.PreparationId);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), history.CurrentPreparation.PreparedBy.Id);
        Assert.IsType<EvidenceRedactionPreparedV2>(Assert.Single(history.Facts));
        Assert.Equal(0, history.LegacyFactCount);
    }

    [Fact]
    public async Task ShouldRetainNativeRegistrationIdentityGivenVerifiedNonIntakeArtifact()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        Assert.True((await DispatchAsync(fixture, fixture.UserId, request)).IsSuccess);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);
        var fact = Assert.IsType<EvidenceRedactionPreparedV2>(Assert.Single(history.Facts));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var sources = await NativeContent.ReadAsync<EvidenceRedactionNativeSources>(content, fixture.TenantId, fact.SourceProofs);

        // Act
        var verified = await scope.ServiceProvider.GetRequiredService<ISourceFactResolver>().VerifyAsync(sources.DerivedRegistration);

        // Assert
        Assert.Equal(derived, fact.Preparation.Derived.ArtifactId);
        Assert.NotEqual(EvidenceIntake.IdFor(fixture.TenantId, new string('b', 64)), derived);
        Assert.Equal(fact.Preparation.Derived.RegistrationEventId, verified.EventId);
        Assert.Equal(fact.Preparation.Derived.RegistrationSha256, verified.PayloadSha256.ToLowerInvariant());
        Assert.Equal(new EvidenceArtifact(fixture.TenantId, derived).Stream, verified.Source);
        Assert.Equal(0, history.LegacyFactCount);
    }

    [Theory]
    [InlineData("same")]
    [InlineData("foreign_tenant")]
    [InlineData("empty_id")]
    [InlineData("digest")]
    [InlineData("event")]
    [InlineData("length")]
    [InlineData("chronology")]
    public void ShouldRejectBeforeAppendGivenInvalidSourceIdentity(string scenario)
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var original = Identity('a');
        var derived = scenario switch
        {
            "same" => original,
            "foreign_tenant" => Identity('b') with { TenantId = Uuid.CreateVersion4() },
            "empty_id" => Identity('b') with { ArtifactId = Uuid.Empty },
            "digest" => Identity('b') with { RegistrationSha256 = "invalid" },
            "event" => Identity('b') with { RegistrationEventId = Uuid.Empty },
            "length" => Identity('b') with { ContentLength = 0 },
            "chronology" => Identity('b') with { RegisteredAt = Now.AddMinutes(1) },
            _ => throw new InvalidOperationException()
        };

        // Act
        var result = redaction.DecidePrepare(Uuid.CreateVersion4(), 0, original, derived, "Manual preparation", "Remove personal fields", Contributor, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents);
    }

    [Fact]
    public async Task ShouldReturnOriginalAttributedPreparationGivenExactRetryAndDenyChangedActor()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var other = await SeedMemberAsync(fixture, BuiltInRbac.StandardUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, other, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        var first = await DispatchAsync(fixture, fixture.UserId, request);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var retry = await DispatchAsync(fixture, fixture.UserId, request);
        var changed = await DispatchAsync(fixture, other, request);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value.Preparations, retry.Value.Preparations);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Single(history.Facts);
    }

    [Fact]
    public async Task ShouldReplayFrozenLineageAndRetainPriorPreparationGivenNewAttributedRevision()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var original = Identity('a');
        var derived = Identity('b');
        var first = Legacy(id, 0, new EvidenceRedactionPreparationFact(Uuid.CreateVersion4(), 1, original, derived,
            "Manual", "First reason", Contributor, Now));
        var second = Legacy(id, 1, new EvidenceRedactionPreparationFact(Uuid.CreateVersion4(), 2, original, derived,
            "Manual", "Revised reason", Contributor, Now.AddMinutes(1)));

        // Act
        var replay = await ReadGivenAsync(null, Tenant, id, first, second);

        // Assert
        Assert.Equal(2, replay.Revision);
        Assert.Equal("First reason", replay.Preparations[0].Reason);
        Assert.Equal("Revised reason", replay.CurrentPreparation!.Reason);
        Assert.Throws<NotSupportedException>(() => ((IList<EvidenceRedactionPreparationFact>)replay.Preparations).Clear());
        Assert.Equal(2, replay.LegacyFactCount);
    }

    [Fact]
    public async Task ShouldRetainExactPreparedApprovalGivenIndependentLeadDecision()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, lead, derived);
        var preparation = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        Assert.True((await DispatchAsync(fixture, fixture.UserId, preparation)).IsSuccess);
        var decision = new ApproveEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, preparation.RedactionId,
            1, preparation.CommandId, 1);

        // Act
        var approved = await DispatchAsync(fixture, lead, decision);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, preparation.RedactionId);

        // Assert
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.Equal(preparation.CommandId, approved.Value.Approvals[0].PreparationId);
        Assert.Equal(1, approved.Value.Approvals[0].PreparedRevision);
        Assert.Equal(RbacIds.Member(fixture.TenantId, lead).ToString(), approved.Value.Approvals[0].ApprovedBy.Id);
        Assert.IsType<EvidenceRedactionApprovedV2>(history.Facts[^1]);
    }

    [Fact]
    public async Task ShouldReturnOriginalPresentationGivenSameCanonicalPreparerAfterDisplayChange()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        var first = await DispatchAsync(fixture, fixture.UserId, request);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var renamed = await DispatchAsync(fixture, fixture.UserId, request, display: "Renamed contributor");
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);

        // Assert
        Assert.True(renamed.IsSuccess, renamed.Error?.Message);
        Assert.Equal(first.Value.Preparations, renamed.Value.Preparations);
        Assert.Equal(first.Value.Preparations[0].PreparedBy, renamed.Value.Preparations[0].PreparedBy);
        Assert.Single(history.Facts);
    }

    [Theory]
    [InlineData("registration_event")]
    [InlineData("registration_payload")]
    [InlineData("content_length")]
    [InlineData("registration_time")]
    public void ShouldRejectChangedSafeSourceTupleGivenRetainedPreparationReplay(string scenario)
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var original = Identity('a');
        var changed = scenario switch
        {
            "registration_event" => original with { RegistrationEventId = Uuid.CreateVersion4() },
            "registration_payload" => original with { RegistrationSha256 = new string('d', 64) },
            "content_length" => original with { ContentLength = 124 },
            "registration_time" => original with { RegisteredAt = Now.AddDays(-2) },
            _ => throw new InvalidOperationException()
        };
        var malformed = Legacy(id, 0, new EvidenceRedactionPreparationFact(Uuid.CreateVersion4(), 1, changed, Identity('b'),
            "Manual", "Private fields", Contributor, Now));

        // Act
        Action replay = () => new AggregateScenario<EvidenceRedaction>(new EvidenceRedaction(Tenant, id)).Given(malformed);

        // Assert
        Assert.Throws<ImmutableContentIntegrityException>(replay);
    }

    [Fact]
    public async Task ShouldPreserveApprovalPresentationAndDenyAnotherCanonicalActorGivenExactRetry()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        var other = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, lead, derived);
        await GrantPairAsync(fixture, other, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        Assert.True((await DispatchAsync(fixture, fixture.UserId, request)).IsSuccess);
        var approval = new ApproveEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, request.RedactionId, 1, request.CommandId, 1);
        var first = await DispatchAsync(fixture, lead, approval);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var renamed = await DispatchAsync(fixture, lead, approval, display: "Renamed lead");
        var changed = await DispatchAsync(fixture, other, approval);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);

        // Assert
        Assert.True(renamed.IsSuccess, renamed.Error?.Message);
        Assert.Equal(first.Value.Approvals, renamed.Value.Approvals);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(2, history.Facts.Count);
    }

    [Fact]
    public async Task ShouldDenySelfApprovalWithoutAppendGivenPreparerAsLead()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, lead, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        Assert.True((await DispatchAsync(fixture, lead, request)).IsSuccess);

        // Act
        var result = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId,
            request.RedactionId, 1, request.CommandId, 1));
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Single(history.Facts);
    }

    [Fact]
    public async Task ShouldPreserveBoundedHistoryAndAllowExactRetryGivenHistoryCapacity()
    {
        // Arrange: the old100-denial assertion is superseded; current native admission has no history-count cap.
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var firstRequest = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Initial");
        var first = await DispatchAsync(fixture, fixture.UserId, firstRequest);
        Assert.True(first.IsSuccess, first.Error?.Message);
        for (var revision = 1; revision < 100; revision++)
            Assert.True((await DispatchAsync(fixture, fixture.UserId, firstRequest with
            {
                CommandId = Uuid.CreateVersion4(),
                ExpectedRevision = revision,
                Reason = "Revision"
            })).IsSuccess);

        // Act
        var retry = await DispatchAsync(fixture, fixture.UserId, firstRequest);
        var extra = await DispatchAsync(fixture, fixture.UserId, firstRequest with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 100,
            Reason = "Extra"
        });
        var replay = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, firstRequest.RedactionId);
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new EvidenceRedaction(fixture.TenantId, firstRequest.RedactionId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value.Preparations, retry.Value.Preparations);
        Assert.True(extra.IsSuccess, extra.Error?.Message);
        Assert.Equal(101, owner.Revision);
        Assert.Equal(101, replay.Preparations.Count);
        Assert.Equal("Initial", replay.Preparations[0].Reason);
        Assert.Equal(101, replay.Facts.Count);
    }

    [Fact]
    public void ShouldDenyBeforeAppendGivenCompleteSerializedPreparationExceedsPayloadBound()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());

        // Act
        var result = redaction.DecidePrepare(Uuid.CreateVersion4(), 0, Identity('a'), Identity('b'),
            new string('\uffff', 4000), new string('\uffff', 4000), Contributor, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents);
    }

    static EvidenceRedactionPrepared Legacy(Uuid id, long expected, EvidenceRedactionPreparationFact value) =>
        DomainEventSeed.Attach(new EvidenceRedactionPrepared(Tenant, id, expected, value), id, (ulong)value.Revision, occurredOn: Now);
    static EvidenceRedactionSourceCapsule Identity(char hash)
    {
        var digest = new string(hash, 64);
        var identity = new EvidenceRedactionSourceCapsule(Tenant, EvidenceIntake.IdFor(Tenant, digest), Uuid.CreateVersion4(),
            new string('c', 64), digest, 123, Now.AddDays(-1), "");
        return identity with { IdentitySha256 = EvidenceRedaction.IdentityDigest(identity) };
    }
}
