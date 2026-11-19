using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEditionOperationsTests
{
    [Fact]
    public async Task ShouldPermitAdmissionGivenNoOptionalLocalReadinessGate()
    {
        // Arrange and act
        var permitted = await CriteriaAuthoringReadiness.CheckAsync(null, CancellationToken.None);

        // Assert
        Assert.True(permitted);
    }

    [Fact]
    public async Task ShouldReturnRetryableUnavailableGivenNoSourceAdmissionAuthority()
    {
        // Arrange
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Uuid.CreateVersion4().ToString(), ClaimValueTypes.String, "bdgrz")], "session"));
        var request = new DraftCriteriaEdition(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "tsc", "edition_1");
        var context = new RequestContext<DraftCriteriaEdition>(request, actor);
        var operations = new CriteriaEditionOperations(null!, null!, null!);

        // Act
        var result = await operations.DraftAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Contains("unavailable", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnRetryableUnavailableBeforeWritingGivenAdmissionIsUnavailable()
    {
        // Arrange
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Uuid.CreateVersion4().ToString()), new Claim("iss", "bdgrz")], "session"));
        var request = new DraftCriteriaEdition(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "tsc", "edition_1");
        var operations = new CriteriaEditionOperations(new EmptyReader(), null!, null!, new UnavailableAuthority(), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.DraftAsync(new RequestContext<DraftCriteriaEdition>(request, actor), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Contains("unavailable", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectEditionWriteBeforeAnyAppendGivenNegativeReadiness()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", user.ToString()), new Claim("iss", "bdgrz")], "session"));
        var request = new DraftCriteriaEdition(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "tsc", "edition_1");
        var reader = new EmptyReader();
        var authority = new AllowedAuthority(user);
        var operations = new CriteriaEditionOperations(reader, null!, null!, authority,
            readiness: new TestCriteriaAuthoringReadiness(false));

        // Act
        var result = await operations.DraftAsync(new RequestContext<DraftCriteriaEdition>(request, actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        Assert.Equal(0, reader.Reads);
        Assert.Equal(0, authority.Calls);
    }

    [Fact]
    public async Task ShouldFailRetryablyGivenReadinessLostBeforeEditionAppend()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", user.ToString()), new Claim("iss", "bdgrz")], "session"));
        var request = new DraftCriteriaEdition(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "tsc", "edition_1");
        var reader = new EmptyReader();
        var authority = new AllowedAuthority(user);
        var operations = new CriteriaEditionOperations(reader, null!, null!, authority,
            readiness: new SequencedCriteriaAuthoringReadiness(true, false));

        // Act
        var result = await operations.DraftAsync(new RequestContext<DraftCriteriaEdition>(request, actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        Assert.Equal(1, reader.Reads);
        Assert.Equal(1, authority.Calls);
    }

    sealed class EmptyReader : IAggregateReader
    {
        public int Reads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            Reads++;
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class AllowedAuthority(Uuid user) : IAccessAdmissionAuthority
    {
        public int Calls { get; private set; }
        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext actor, Uuid operationId,
            string target, string intentSha256, CancellationToken ct = default)
        {
            Calls++;
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            return ValueTask.FromResult<AccessAdmissionEvidence?>(new AccessAdmissionEvidence(user, user.ToString(), "bdgrz",
                operationId, target, intentSha256, DateTimeOffset.UtcNow, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            });
        }
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class UnavailableAuthority : IAccessAdmissionAuthority
    {
        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext actor, Uuid operationId,
            string target, string intentSha256, CancellationToken ct = default) => throw new AccessAdmissionUnavailableException();
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
