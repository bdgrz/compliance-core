using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class RegisterTenantHandler(IAggregateExecutor executor, PlatformOperatorAuthority operators)
    : IRequestHandler<RegisterTenant, TenantRegistration>
{
    public ValueTask<Result<TenantRegistration>> HandleAsync(
        IRequestContext<RegisterTenant> context,
        CancellationToken ct)
    {
        if (context.Request.TenantId == Uuid.Empty)
            return ValueTask.FromResult(Result<TenantRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "A stable tenant identity is required for organization creation.")));
        // RegisterTenantAuthorizer guarantees this before the handler ever runs.
        var ownerUserId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "RegisterTenantAuthorizer must reject requests without a Bdgrz user identity.");

        if (string.IsNullOrWhiteSpace(context.Request.LegalName) && !operators.DeveloperAuthentication)
            return ValueTask.FromResult(Result<TenantRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "A legal name is required.")));
        if (context.Request.FirstAdministratorEmail is not null)
            return ValueTask.FromResult(Result<TenantRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "First administrator invitations are not supported for self-service creation.")));

        return executor.ExecuteAsync(
            new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(
                tenant.Register(ownerUserId, context.Request.Name, context.Request.Slug,
                    context.Request.LegalName, context.Request.FirstAdministratorEmail,
                    creatorIsAdministrator: true)),
            context, ct);
    }
}
