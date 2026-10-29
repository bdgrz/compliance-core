using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     A restricted field value, or an explicit redaction. A redacted field holds no value, so reading
///     <see cref="Value" /> fails instead of yielding a default that could pass for data.
/// </summary>
public sealed class RestrictedField<T>
{
    readonly T _value;

    RestrictedField(T value, bool isRedacted)
    {
        _value = value;
        IsRedacted = isRedacted;
    }

    internal static RestrictedField<T> Redacted { get; } = new(default!, isRedacted: true);

    public bool IsRedacted { get; }

    public T Value => IsRedacted
        ? throw new InvalidOperationException("The field is redacted for this actor.")
        : _value;

    internal static RestrictedField<T> Visible(T value) => new(value, isRedacted: false);
}
