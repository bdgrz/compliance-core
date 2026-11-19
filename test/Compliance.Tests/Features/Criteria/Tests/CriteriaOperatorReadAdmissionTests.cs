using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaOperatorReadAdmissionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldNotReadGivenCurrentAuthorityIsUnavailable(bool draft)
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var reader = new Reader();

        // Act
        var result = await ReadAsync(draft, user, edition, operation, reader, null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldNotReadGivenAdmissionBindsAnotherTarget(bool draft)
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var reader = new Reader();
        var authority = new Authority(user) { WrongTarget = true };

        // Act
        var result = await ReadAsync(draft, user, Uuid.CreateVersion4(), Uuid.CreateVersion4(), reader, authority);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRecheckAuthorityGivenEachReadCanObserveChanges(bool draft)
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var reader = new Reader();
        var authority = new Authority(user);

        // Act
        var first = await ReadAsync(draft, user, edition, operation, reader, authority);
        authority.Deny = true;
        var second = await ReadAsync(draft, user, edition, operation, reader, authority);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, second.Error!.Kind);
        Assert.Equal(2, authority.Calls);
        Assert.Equal(1, reader.Calls);
        var expected = draft ? CriteriaEdition.AddressOf(edition) : CriteriaEntry.StreamFor(edition, "CC6.1");
        Assert.Equal(expected.ToString(), authority.LastTarget);
    }

    [Fact]
    public async Task ShouldSeparateReadPurposesGivenTheSameTargetAndOperation()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", user.ToString()), new Claim("iss", "bdgrz")], "session"));
        var target = CriteriaEntry.StreamFor(edition, "CC6.1");
        var authority = new Authority(user);

        // Act
        var first = await CriteriaOperatorReadAdmission.AuthorizeAsync(authority, new RequestDispatchContext(actor), edition, operation, target, "operation");
        var firstDigest = authority.LastDigest;
        var second = await CriteriaOperatorReadAdmission.AuthorizeAsync(authority, new RequestDispatchContext(actor), edition, operation, target, "edition-value-operation");

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(firstDigest, authority.LastDigest);
    }

    static async ValueTask<Result> ReadAsync(bool draft, Uuid user, Uuid edition, Uuid operation,
        Reader reader, IAccessAdmissionAuthority? authority)
    {
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", user.ToString()), new Claim("iss", "bdgrz")], "session"));
        if (draft)
        {
            var result = await new GetCriteriaEditionDraftHandler(reader, authority).HandleAsync(
                new RequestContext<GetCriteriaEditionDraft>(new GetCriteriaEditionDraft(edition), actor), CancellationToken.None);
            return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
        }
        var receipt = await new GetCriteriaOperationHandler(reader, authority).HandleAsync(
            new RequestContext<GetCriteriaOperation>(new GetCriteriaOperation(edition, operation, "CC6.1"), actor), CancellationToken.None);
        return receipt.IsSuccess ? Result.Success : Result.Failure(receipt.Error!);
    }

    sealed class Reader : ICriteriaOperationReader
    {
        public int Calls { get; private set; }
        public ValueTask<Result<CriteriaEditionDraftView>> GetDraftAsync(Uuid editionId, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(Result<CriteriaEditionDraftView>.Success(new CriteriaEditionDraftView(editionId, "draft", "edition_1", null)));
        }
        public ValueTask<Result<CriteriaOperationReceipt>> GetOperationAsync(Uuid editionId, Uuid operationId,
            string? identifier, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(Result<CriteriaOperationReceipt>.Success(new CriteriaOperationReceipt(editionId, operationId, "accepted")));
        }
    }

    sealed class Authority(Uuid user) : IAccessAdmissionAuthority
    {
        public bool WrongTarget { get; init; }
        public bool Deny { get; set; }
        public int Calls { get; private set; }
        public string? LastTarget { get; private set; }
        public string? LastDigest { get; private set; }
        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext actor, Uuid operationId,
            string target, string intentSha256, CancellationToken ct = default)
        {
            Calls++;
            LastTarget = target;
            LastDigest = intentSha256;
            if (Deny)
                return ValueTask.FromResult<AccessAdmissionEvidence?>(null);
            var version = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var evidence = new AccessAdmissionEvidence(user, user.ToString(), "bdgrz", operationId,
                WrongTarget ? "stream://bdgrz/other/target" : target, intentSha256,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", version, version)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = version
            };
            return ValueTask.FromResult<AccessAdmissionEvidence?>(evidence);
        }
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
