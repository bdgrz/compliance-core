using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.Work;

/// <summary>The authored retry evidence; owner and command identities come from the canonical route.</summary>
sealed record WorkDigestRetryHttpBody(
    [property: JsonRequired] string EvidenceReference,
    [property: JsonRequired] string Rationale,
    [property: JsonRequired] long ExpectedRevision);
