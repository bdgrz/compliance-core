using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEditionValueReceiptHandlerTests
{
    [Fact]
    public async Task ShouldNotReadGivenCurrentOperatorAuthorityIsUnavailable()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var reader = new Reader();
        var handler = new GetCriteriaEditionValueOperationHandler(reader);
        var context = Context(user);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task ShouldRecheckAuthorityGivenEachReceiptCanObserveChanges()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var reader = new Reader();
        var admission = new Authority(user);
        var handler = new GetCriteriaEditionValueOperationHandler(reader, admission);
        var context = Context(user);

        // Act
        var first = await handler.HandleAsync(context, CancellationToken.None);
        admission.Deny = true;
        var revoked = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.False(revoked.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, revoked.Error!.Kind);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(2, admission.Calls);
    }

    [Fact]
    public async Task ShouldRejectGivenAdmissionIsBoundToAnotherTarget()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var reader = new Reader();
        var authority = new Authority(user) { WrongTarget = true };
        var handler = new GetCriteriaEditionValueOperationHandler(reader, authority);

        // Act
        var result = await handler.HandleAsync(Context(user), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal(0, reader.Calls);
    }

    static RequestContext<GetCriteriaEditionValueOperation> Context(Uuid user)
    {
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", user.ToString()), new Claim("iss", "bdgrz")], "session"));
        return new RequestContext<GetCriteriaEditionValueOperation>(new GetCriteriaEditionValueOperation(
            Uuid.CreateVersion4(), "field", "coverage_note", null, null, Uuid.CreateVersion4()), actor);
    }

    sealed class Reader : ICriteriaEditionValueOperationReader
    {
        public int Calls { get; private set; }
        public ValueTask<Result<CriteriaOperationReceipt>> ReadAsync(Uuid editionId, string targetKind, string? field,
            string? category, string? code, Uuid operationId, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(Result<CriteriaOperationReceipt>.Success(new CriteriaOperationReceipt(editionId, operationId, "accepted")));
        }
    }

    sealed class Authority(Uuid user) : IAccessAdmissionAuthority
    {
        public bool Deny { get; set; }
        public bool WrongTarget { get; init; }
        public int Calls { get; private set; }
        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext actor, Uuid operationId,
            string target, string intentSha256, CancellationToken ct = default)
        {
            Calls++;
            if (Deny)
                return ValueTask.FromResult<AccessAdmissionEvidence?>(null);
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var evidence = new AccessAdmissionEvidence(user, user.ToString(), "bdgrz", operationId,
                WrongTarget ? "stream://bdgrz/other/target" : target, intentSha256,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
            return ValueTask.FromResult<AccessAdmissionEvidence?>(evidence);
        }
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
