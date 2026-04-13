using System.ComponentModel.DataAnnotations.Schema;
using TTH.Areas.Super.Data.Operation_Team;

namespace TTH.Areas.Super.Data.TeamAttendance
{
    public class TeamMemberWeekendOff
    {
        public int Id { get; set; }
        public int TeamWeekendOffId { get; set; }

        [ForeignKey("TeamWeekendOffId")]
        public TeamWeekendOff TeamWeekendOff { get; set; }
        public string Weekend { get; set; }

        public string? EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}