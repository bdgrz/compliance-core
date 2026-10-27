using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>One actor's read decision for one field class, resolved once and applied to many values.</summary>
public sealed class FieldRedactor
{
    internal FieldRedactor(FieldClass fieldClass, bool canRead)
    {
        FieldClass = fieldClass;
        CanRead = canRead;
    }

    public FieldClass FieldClass { get; }

    public bool CanRead { get; }

    public RestrictedField<T> Apply<T>(T value) =>
        CanRead ? RestrictedField<T>.Visible(value) : RestrictedField<T>.Redacted;
}
