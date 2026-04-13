using TTH.Areas.Super.Data.TeamAttendance;

namespace TTH.Areas.Super.Models.TeamAttendance
{
    public class TeamHolidaysViewModel
    {
        public List<EmployeeHolidays> UpcomingHolidays { get; set; } = new List<EmployeeHolidays>();
        public List<TeamLeavesType> LeaveTypes { get; set; } = new List<TeamLeavesType>();

      
    }
}
