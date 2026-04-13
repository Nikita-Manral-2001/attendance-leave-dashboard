using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Models.TeamAttendance
{
    public class EmployeeLeaveDocument
    {
        [Key]
        public int DocumentId { get; set; }
        public int EmployeeLeaveId { get; set; }
        public string EmployeeId { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FilePath { get; set; }

        public long FileSize { get; set; } 
        public DateTime UploadedOn { get; set; }
    }
}
