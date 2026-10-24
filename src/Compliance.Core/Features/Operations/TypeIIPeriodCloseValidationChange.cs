using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Complete immutable server reconciliation, separate from the native acceptance envelope.</summary>
public sealed record TypeIIPeriodCloseValidationChange(DurableIntentHeader Header, TypeIIPeriodCloseValidationContent Content)
{
    public const string Schema = "type-ii-period-close-validation-change.v1";
}
