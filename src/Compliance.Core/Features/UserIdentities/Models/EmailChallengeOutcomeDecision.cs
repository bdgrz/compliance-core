using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>An outcome kind with the record it leaves behind.</summary>
public sealed record EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind Kind, EmailChallengeDeliveryRecord Record);
