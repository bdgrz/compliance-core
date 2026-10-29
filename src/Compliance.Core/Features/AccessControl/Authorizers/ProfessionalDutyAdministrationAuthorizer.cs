using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
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
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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
