using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Whether a governed reference may name a system instance now, later, or not at all.</summary>
public enum SystemInstanceReferenceState
{
    Missing,
    /// <summary>The source exists but the tenant instance projection has not reached it.</summary>
    Pending,
    Declared,
}
