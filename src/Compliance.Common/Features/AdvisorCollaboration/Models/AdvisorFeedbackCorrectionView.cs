using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>A linked, immutable correction to previously posted advisor feedback.</summary>
public sealed record AdvisorFeedbackCorrectionView(Uuid CorrectionId, string CorrectedBody,
    string CorrectionReason, ActorReference CorrectedBy, DateTimeOffset CorrectedAt);
