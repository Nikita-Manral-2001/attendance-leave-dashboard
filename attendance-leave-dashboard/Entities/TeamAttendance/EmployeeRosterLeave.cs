namespace TTH.Areas.Super.Data.TeamAttendance
{
    public class EmployeeRosterLeave
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime RosterDate { get; set; }
        public DateTime LeaveDate { get; set; }
        public string? Note { get; set; }
    }
}