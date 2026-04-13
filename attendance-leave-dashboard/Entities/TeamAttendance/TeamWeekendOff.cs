using TTH.Areas.Super.Data.Operation_Team;

namespace TTH.Areas.Super.Data.TeamAttendance
{
    public class TeamWeekendOff
    {
        public int Id { get; set; }
        public string Weekend { get; set; }
        public string SeasoneOrYear { get; set; }
        public string? Seasone { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string WeekendDays { get; set; }
        public List<TeamMemberWeekendOff> TeamMemberWeekendOff { get; set; }
    }
}