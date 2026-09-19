namespace PropLink.Domain.Enums;

public enum InspectionStatus
{
    PendingSellerApproval,
    PendingAgentSchedule,
    ScheduleFixed,
    InspectionCompleted,
    UnderProcessing,
    DeclinedBySeller,
    Cancelled
}
