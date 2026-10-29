using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Rules;

/// <summary>Routes responsibility scope validation to the feature that owns the record type.</summary>
sealed class SourceRecordResponsibilityScopeValidator(
    BoundaryResponsibilityScopeValidator boundaries,
    ControlResponsibilityScopeValidator controls,
    CommitmentResponsibilityScopeValidator commitments) : IResponsibilityScopeValidator
{
    public ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.RecordType switch
        {
            SeparationOfDutiesRecordTypes.Boundary => boundaries.ValidateAsync(tenantId, scope, ct),
            SeparationOfDutiesRecordTypes.Control => controls.ValidateAsync(tenantId, scope, ct),
            SeparationOfDutiesRecordTypes.Commitment => commitments.ValidateAsync(tenantId, scope, ct),
            _ => ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The responsibility record type is not supported."))),
        };
    }
}
