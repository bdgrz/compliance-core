using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

/// <summary>Shared pieces of the five item writes: root provenance and the append-time guard of the item owner.</summary>
static class AccessReviewItemWrites
{
    /// <summary>The provenance of a root item operation on the item's own stream.</summary>
    public static OperationProvenance Provenance(AccessReviewItemRead read, Uuid commandId, string commandKind,
        ActorReference actor, long expectedRevision, string intentDigest, DateTimeOffset now) =>
        AccessReviewEffects.Root(commandId, commandKind, read.Item.Stream, actor, expectedRevision, intentDigest, now);

    /// <summary>The not-found refusal every item write uses.</summary>
    public static RequestError ItemNotFound() => new(RequestErrorKind.NotFound, "The review item was not found.");
}
