using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>A linked, immutable correction to previously posted advisor feedback.</summary>
public sealed record AdvisorFeedbackCorrectionView(Uuid CorrectionId, string CorrectedBody,
    string CorrectionReason, ActorReference CorrectedBy, DateTimeOffset CorrectedAt);
