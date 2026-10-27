using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskAcceptanceIdentityTests
{
    [Theory]
    [InlineData("rationale")]
    [InlineData("expiry")]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("waiver")]
    public async Task ShouldBindAllOriginalAcceptanceFieldsGivenLaterResidualAssessment(string change)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var assessor = Uuid.CreateVersion4();
        var approverUser = Uuid.CreateVersion4();
        var approver = RbacIds.Member(tenant, approverUser);
        var residual = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMonths(6);
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(approver, "Author"), now));
        var owner = new RiskEvaluationRules(tenant, risk, command);
        Assert.Null(owner.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
            "Inherent", assessor, "Assessor", now));
        Assert.Null(owner.ChooseTreatment(program, 1, "accept", "Accept within tolerance", assessor, "Author", now));
        Assert.Null(owner.RecordAssessment(program, 2, residual, method.Current!, "residual", 2, 2,
            "Residual", assessor, "Assessor", now));
        var request = new AcceptRisk(command, tenant, program, risk, 3, residual, "compliance_lead", expires, "Original acceptance");
        Assert.Null(owner.Accept(program, 3, command, residual, method.Current!, "compliance_lead", true,
            expires, request.Rationale, approver, "Original approver", now));
        Assert.Null(owner.RecordAssessment(program, 4, Uuid.CreateVersion4(), method.Current!, "residual", 1, 1,
            "Later residual", assessor, "Assessor", now));
        await using var source = new RiskEvaluationSourceFixture();
        await source.SeedAsync(owner);
        var facts = await source.FactsAsync(tenant, risk);
        var hydrated = await source.Evaluations.ReadAsync(tenant, program, risk);
        var permissions = new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true));
        var governance = new RiskGovernanceReader(source.Reader, source.Events, source.Content);
        var waivers = new SeparationOfDutiesWaiverProofResolver(source.Reader, new EventStoreSourceFactResolver(source.Events));

        // Act
        var original = await source.Operations().AcceptAsync(RiskEvaluationSourceFixture.Context(request, approverUser), permissions, governance, waivers, CancellationToken.None);
        var changed = await source.Operations().AcceptAsync(RiskEvaluationSourceFixture.Context(request with
        {
            Rationale = change == "rationale" ? "Changed acceptance" : request.Rationale,
            ExpiresAt = change == "expiry" ? expires.AddDays(1) : expires,
            ExpectedRevision = change == "revision" ? 4 : request.ExpectedRevision,
            SeparationOfDutiesWaiverId = change == "waiver" ? Uuid.CreateVersion4() : null,
        }, change == "actor" ? Uuid.CreateVersion4() : approverUser), permissions, governance, waivers, CancellationToken.None);
        var result = Assert.IsType<Result<RiskAcceptanceView>>(original);
        var conflict = Assert.IsType<Result<RiskAcceptanceView>>(changed);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(residual, result.Value.ResidualAssessmentId);
        Assert.Equal("Original approver", result.Value.Approver.Display);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error?.Kind);
        Assert.Equal(5, hydrated.Revision);
        Assert.Equal(facts.Count, (await source.FactsAsync(tenant, risk)).Count);
    }
}
