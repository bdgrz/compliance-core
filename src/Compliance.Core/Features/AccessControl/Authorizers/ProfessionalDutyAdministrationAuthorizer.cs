using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Operators may relay verified issuer assertions but cannot create or confer professional duty.</summary>
sealed class ProfessionalDutyAdministrationAuthorizer(IPlatformOperatorAccess operators,
    IFirmProfessionalDutyAssertionVerifier assertions)
    : IRequestAuthorizer<IProfessionalDutyAdministrationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IProfessionalDutyAdministrationRequest> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor))
            return Deny(RequestErrorKind.Forbidden,
                "Professional-duty evidence requires a personal HTTP request.");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized,
                "Professional-duty evidence requires a canonical signed-in operator.");
        if (!await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden,
                "Only a current platform operator may relay signed professional-duty evidence.");

        return context.Request switch
        {
            GetFirmProfessionalDutyCatalog => Result.Success,
            RecordFirmProfessionalDutyDesignation request when assertions.TryVerifyBinding(request.SignedAssertion, out var assertion) && assertion.Status == "active" &&
                assertion.DesignationId == request.DesignationId &&
                assertion.AssertionId == request.AssertionId => Result.Success,
            RevokeFirmProfessionalDutyDesignation request when assertions.TryVerifyBinding(request.SignedAssertion, out var assertion) && assertion.Status == "revoked" &&
                assertion.DesignationId == request.DesignationId &&
                assertion.AssertionId == request.AssertionId => Result.Success,
            _ => Deny(RequestErrorKind.Forbidden,
                "Duty changes require a cryptographically verified assertion from the configured firm-authority issuer.")
        };
    }

    static Result Deny(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
