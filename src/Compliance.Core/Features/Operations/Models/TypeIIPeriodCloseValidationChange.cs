using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Complete immutable server reconciliation, separate from the native acceptance envelope.</summary>
public sealed record TypeIIPeriodCloseValidationChange(DurableIntentHeader Header, TypeIIPeriodCloseValidationContent Content)
{
    public const string Schema = "type-ii-period-close-validation-change.v1";
}
