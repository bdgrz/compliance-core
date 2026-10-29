using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>The canonical authored content of one admitted comment: normalized body and compartment with the original actor.</summary>
public sealed record AuditorCommentContent(Uuid ProgramId, Uuid RequestId, Uuid CommentId, string Body,
    string Compartment, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset At);
