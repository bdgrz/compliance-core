using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Policies.Models;
namespace Bdgrz.Compliance.Features.Policies.Rules;

/// <summary>Compact discovery identity at one native source cut, including a discarded identity tombstone.</summary>
public sealed record PolicyDirectoryRow(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, PolicyAuthorityHeader Header, long NativeSequence);
