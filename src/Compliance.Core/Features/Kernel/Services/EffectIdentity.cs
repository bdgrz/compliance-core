using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Kernel.Services;

/// <summary>Deterministic identities for effects caused by a source event.</summary>
public static class EffectIdentity
{
    /// <summary>Command id of one effect of a source event; a null episode means the initial episode.</summary>
    public static Uuid CommandId(Uuid sourceEventId, string effectKind, string target, string? episode) =>
        Uuid.CreateVersion5(sourceEventId, Encoding.UTF8.GetBytes($"{effectKind}:{target}:{episode ?? "initial"}"));
}
