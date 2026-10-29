using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>
///     Declares an application as the root native acceptance of its client-chosen identity on the application's own
///     stream. An exact retry replays the original registration after any later change; a changed creation, actor or
///     reserved identity conflicts without an append.
/// </summary>
public sealed class DeclareApplicationHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock, RestrictedApplicationVisibility visibility,
    ApplicationGovernedDeclarationGate? gate = null)
    : IRequestHandler<DeclareApplication, ApplicationRegistration>
{
    const string CommandKind = "DeclareApplication";

    public async ValueTask<Result<ApplicationRegistration>> HandleAsync(
        IRequestContext<DeclareApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.ApplicationId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Invalid("An application declaration requires its application and tenant identity.");
        if (request.SystemOwnerPersonId == Uuid.Empty || request.AccessOwnerPersonId == Uuid.Empty)
            return Invalid("Application owners must identify recorded workforce people in this tenant.");
        var governed = gate?.IsEnabled ?? false;
        var current = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        // Restricted existence is never disclosed to a caller who may not read the application, whether the
        // application is found now or appears between this read and the append.
        var canReadRestricted = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) &&
                                await visibility.HasRestrictedApplicationPermissionAsync(request.TenantId, userId,
                                    request.ApplicationId, ct).ConfigureAwait(false);
        if (Hidden(current, canReadRestricted))
            return Result<ApplicationRegistration>.Failure(NotFound);

        var provenance = ApplicationInventoryOperations.Provenance(request.ApplicationId, CommandKind,
            current.Stream, memberId, null, AuthoredIntentDigest.Compute(Canonical(request)), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var owners = await ApplicationOwnerReferences.ValidateAsync(reader, request.TenantId,
                request.SystemOwnerPersonId, request.AccessOwnerPersonId, ct).ConfigureAwait(false);
            if (owners is not null)
                return Result<ApplicationRegistration>.Failure(owners);
            response = await ApplicationInventoryOperations.RegistrationAsync(content, request.TenantId,
                ApplicationInventoryOperations.ApplicationRegistrationSchema,
                new ApplicationRegistration(request.ApplicationId), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<DeclaredApplication>(binding,
            () => Owner(request), app => app.Acceptance,
            app => Hidden(app, canReadRestricted)
                ? Result<FirstResponseDescriptor>.Failure(NotFound)
                : ApplicationInventoryOperations.Prepared(app.PrepareDeclare(request.Name, request.Purpose,
                    request.OwnerReference, request.Classification, request.SystemOwnerPersonId,
                    request.AccessOwnerPersonId, request.ApplicationKind, request.Criticality, governed), response),
            (DeclaredApplication app, ImmutableContentRef frozen) => Hidden(app, canReadRestricted)
                ? Result.Failure(NotFound)
                : app.Declare(provenance, frozen, request.Name,
                    request.Purpose, request.OwnerReference, memberId, display, provenance.AcceptedAt,
                    request.Classification, request.SystemOwnerPersonId, request.AccessOwnerPersonId,
                    request.IsRestricted, request.ApplicationKind, request.Criticality, governed)), context, ct)
            .ConfigureAwait(false);
        return await ApplicationImportContent.ResponseAsync<ApplicationRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    /// <summary>The authored intent as the owner reads it: text is trimmed and blank optional text is absent.</summary>
    static DeclareApplication Canonical(DeclareApplication request) => request with
    {
        Name = request.Name?.Trim()!,
        Purpose = request.Purpose?.Trim()!,
        OwnerReference = ApplicationInventoryOperations.Optional(request.OwnerReference),
        Classification = ApplicationInventoryOperations.Optional(request.Classification),
        ApplicationKind = ApplicationInventoryOperations.Optional(request.ApplicationKind),
        Criticality = ApplicationInventoryOperations.Optional(request.Criticality),
    };

    static RequestError NotFound { get; } = new(RequestErrorKind.NotFound, "The application was not found.");

    static bool Hidden(DeclaredApplication app, bool canReadRestricted) =>
        app.IsCreated && app.IsRestricted && !canReadRestricted;

    static DeclaredApplication Owner(DeclareApplication request) =>
        new(request.TenantId, request.ApplicationId, request.ApplicationId);

    static Result<ApplicationRegistration> Invalid(string message) =>
        Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Validation, message));
}
