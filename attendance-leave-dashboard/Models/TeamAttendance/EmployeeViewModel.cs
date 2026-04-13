using TTH.Areas.Super.Data.TeamAttendance;

namespace TTH.Areas.Super.Models.TeamAttendance
{
    public class EmployeeViewModel
    {
        public string EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime? JoinDate { get; set; }
        public DateTime? ResignDate { get; set; }
        public List<EmployeeViewModel> Employees { get; set; }
        public List<EmployeeViewModel> OperationEmployees { get; set; }
        public List<EmployeeLeaves> Leaves { get; set; }
        public List<EmployeeRosterLeave> Roster { get; set; }
        public List<TeamWeekendOff> TeamWeekendLeaves { get; set; }

    }
}
