using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Rules;

/// <summary>Validates responsibility scopes through the feature that owns the source record.</summary>
public interface IResponsibilityScopeValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default);
}
