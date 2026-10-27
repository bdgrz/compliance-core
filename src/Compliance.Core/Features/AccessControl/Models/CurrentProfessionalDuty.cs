namespace Bdgrz.Compliance.Features.AccessControl.Models;

sealed record CurrentProfessionalDuty(FirmStaffMemberView Staff,
    FirmProfessionalDutyDesignationView Designation, long ContinuityStartRevision);
