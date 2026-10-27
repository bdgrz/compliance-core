using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Loads caught-up canonical facts for comparison without disclosing them.</summary>
public sealed class WorkforceSourceTargets(PersonReadConsistency people,
    WorkRelationshipReadConsistency relationships, ServiceIdentityReadConsistency identities)
{
    public async ValueTask<Result<WorkforceSourceTarget>> GetAsync(Uuid tenantId, string targetKind,
        Uuid targetId, CancellationToken ct)
    {
        switch (targetKind)
        {
            case "person":
                var person = await people.GetAsync(tenantId, targetId, null, ct).ConfigureAwait(false);
                return person.IsSuccess
                    ? Result<WorkforceSourceTarget>.Success(new WorkforceSourceTarget(person.Value.Revision,
                        new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts(person.Value.DisplayName, person.Value.WorkEmail))))
                    : Result<WorkforceSourceTarget>.Failure(person.Error);
            case "work_relationship":
                var job = await relationships.GetAsync(tenantId, targetId, null, ct).ConfigureAwait(false);
                return job.IsSuccess
                    ? Result<WorkforceSourceTarget>.Success(new WorkforceSourceTarget(job.Value.Revision,
                        new WorkforceSourceFacts(WorkRelationship: new WorkRelationshipTerms(job.Value.WorkerType,
                            job.Value.LifecycleStatus, job.Value.StartDate, job.Value.EndDate, job.Value.Department,
                            job.Value.ManagerPersonId, job.Value.SponsorPersonId, job.Value.EmploymentStatusReason))))
                    : Result<WorkforceSourceTarget>.Failure(job.Error);
            case "service_identity":
                var identity = await identities.GetAsync(tenantId, targetId, null, ct).ConfigureAwait(false);
                return identity.IsSuccess
                    ? Result<WorkforceSourceTarget>.Success(new WorkforceSourceTarget(identity.Value.Revision,
                        new WorkforceSourceFacts(ServiceIdentity: new WorkforceServiceSourceFacts(identity.Value.DisplayName,
                            identity.Value.IdentityKind, identity.Value.Environment, identity.Value.LifecycleStatus, identity.Value.ExpiresOn))))
                    : Result<WorkforceSourceTarget>.Failure(identity.Error);
            default:
                return Result<WorkforceSourceTarget>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The target kind must be person, work_relationship, or service_identity."));
        }
    }
}
