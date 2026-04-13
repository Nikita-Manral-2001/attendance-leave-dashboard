namespace TTH.Areas.Super.Data.TeamAttendance
{
    public class EmployeeLeaves
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Leave { get; set; }
        public string? LeaveType { get; set; }
        public string? Position { get; set; }
        public string? Located { get; set; }
        public string? Note { get; set; }
        public string? NoteByHR { get; set; }
        public string? HRApproval { get; set; }
        public string? Approval { get; set; }
        public string? ApprovedBy { get; set; }
        public double PendingLeaves { get; set; }
    }
}