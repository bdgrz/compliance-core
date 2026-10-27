namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>One exact source and current content proposed for sharing.</summary>
public sealed record AdvisorHandoffPreviewItemView(AdvisorRecordVersion SelectedSource,
    string Change, AdvisorRecordVersion? ComparedSource, AdvisorSharedSourceView Content);
