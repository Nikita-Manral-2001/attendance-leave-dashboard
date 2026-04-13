namespace TTH.Areas.Super.Models.TeamAttendance
{
    public class EmployeeLeaveModel
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; }
        public string FullName { get; set; }
        public string StartDate { get; set; } // Use string to parse in controller
        public string EndDate { get; set; }
        public string LeaveType { get; set; }
        public string Leave { get; set; }
        public string? Note { get; set; }
        public string? HRApproval { get; set; }
    }
}
