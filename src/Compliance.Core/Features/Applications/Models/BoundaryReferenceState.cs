using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Keys needed to replace current references without scanning a tenant's boundaries.</summary>
sealed record BoundaryReferenceState(Uuid BoundaryId, Uuid ProgramId,
    Uuid? DraftVersionId, long DraftRevision, IReadOnlyList<Uuid> DraftEntryIds,
    Uuid? ApprovedVersionId, IReadOnlyList<Uuid> ApprovedEntryIds);
