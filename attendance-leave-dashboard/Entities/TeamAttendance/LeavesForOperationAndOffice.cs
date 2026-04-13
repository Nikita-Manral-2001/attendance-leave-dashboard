namespace TTH.Areas.Super.Data.TeamAttendance
{
    public class LeavesForOperationAndOffice
    {
        public int Id { get; set; }


        public int OperationValue { get; set; }


        public int OfficeValue { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

    }
}
