using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.Providers.Services;

/// <summary>Inspects authoritative canonical records; snapshots are observations, not cross-stream approvals.</summary>
public sealed class ProviderReferences(IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<ProviderContent>> ResolveAsync(Uuid tenantId,
        ProviderContent content, CancellationToken ct)
    {
        if (ProviderRules.InputError(content) is { } inputError)
            return Result<ProviderContent>.Failure(new RequestError(RequestErrorKind.Validation, inputError));
        content = ProviderRules.Normalize(content with { OwnerPersonRevision = null });
        if (ProviderRules.Validate(content) is { } validation)
            return Result<ProviderContent>.Failure(new RequestError(RequestErrorKind.Validation, validation));
        if (content.OwnerPersonId is { } ownerId)
        {
            var owner = await reader.HydrateAsync(new Person(tenantId, ownerId), ct).ConfigureAwait(false);
            if (!owner.IsCreated)
                return Missing();
            content = content with { OwnerPersonRevision = owner.Revision };
        }
        var dependencies = new List<ProviderDependency>();
        foreach (var requested in content.Dependencies!)
        {
            var dependency = requested with { SourceRevision = null, ProgramRevision = null, ApplicationRevision = null };
            if (dependency.SubjectId is { } id)
            {
                if (dependency.SubjectKind == "client_service")
                {
                    var program = await reader.HydrateAsync(new ComplianceProgram(tenantId, dependency.ProgramId!.Value), ct).ConfigureAwait(false);
                    var service = await reader.HydrateAsync(new ClientService(tenantId, id), ct).ConfigureAwait(false);
                    if (!program.IsCreated || !service.IsCreated || service.ProgramId != dependency.ProgramId)
                        return Missing();
                    dependency = dependency with { SourceRevision = service.Revision, ProgramRevision = program.Revision };
                }
                else
                {
                    var applicationId = dependency.ApplicationId!.Value;
                    var application = await reader.HydrateApplicationAsync(tenantId, applicationId, ct).ConfigureAwait(false);
                    var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, tenantId, applicationId, id, ct).ConfigureAwait(false);
                    if (!application.IsCreated || instance is null)
                        return Missing();
                    dependency = dependency with { SourceRevision = instance.Revision, ApplicationRevision = application.Revision };
                }
            }
            dependencies.Add(dependency);
        }
        content = content with { Dependencies = dependencies.AsReadOnly() };
        var inspectedArtifacts = new HashSet<Uuid>();
        foreach (var citation in content.Dependencies!.Select(static dependency => dependency.SourceCitation).Prepend(content.SourceCitation))
        {
            if (citation?.ArtifactId is { } artifactId && inspectedArtifacts.Add(artifactId) &&
                !(await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct).ConfigureAwait(false)).IsCreated)
                return Missing();
        }
        return Result<ProviderContent>.Success(content);
    }

    static Result<ProviderContent> Missing() => Result<ProviderContent>.Failure(new RequestError(
        RequestErrorKind.NotFound, "A canonical provider reference was not found in its owning context."));
}
