using ClosedXML.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using sib_api_v3_sdk.Model;
using System.Security.Claims;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Data.Rent;
using TTH.Areas.Super.Data.TeamAttendance;
using TTH.Areas.Super.Models.TeamAttendance;
using TTH.Areas.Super.Repository.RentRepository;
using TTH.Service;

namespace TTH.Areas.Super.Controllers
{
    [Area("super")]
    [Route("super/[controller]")]
    [Authorize(Roles = "Super,HR,Admin,OperationHead,DigitalTeamLead,General")]
    public class TeamAttendanceController : Controller
    {
        private readonly AppDataContext _context;
        private readonly IConfiguration _configuration;
        private readonly IBravoMail _bravoMail;


        public TeamAttendanceController(AppDataContext context,IConfiguration configuration, IBravoMail bravoMail)
        {
            _context = context;
            _configuration = configuration;
            _bravoMail = bravoMail;
        }
        [HttpGet]
        [Route("EmployeeLeaveTracker")]
        public IActionResult EmployeeLeaveTracker()
        {
            var today = DateTime.Today;

            var viewModel = new TeamHolidaysViewModel
            {
                UpcomingHolidays = _context.EmployeeHolidays
            .Where(e => e.Date >= today)
            .OrderBy(e => e.Date)
            .ToList(),
                LeaveTypes = _context.TeamLeavesType
            .OrderBy(t => t.LeaveTypeName)
            .ToList()
            };

            return View(viewModel);
        }
        [HttpPost]
        [Route("EmployeeLeaveTracker")]
        public IActionResult EmployeeLeaveTracker(List<DateTime> holidayDates, List<string> holidayNames)
        {
            for (int i = 0; i < holidayDates.Count; i++)
            {
                DateTime date = holidayDates[i];
                string holiday = holidayNames[i];

                var holidayEntry = new EmployeeHolidays
                {
                    Date = date,
                    Holidays = holiday
                };

                _context.EmployeeHolidays.Add(holidayEntry);
            }

            _context.SaveChanges();



            return RedirectToAction("EmployeeLeaveTracker");
        }

        [HttpPost]
        public async Task<IActionResult> AddLeaveType(string leaveType, double dayValue, string colorPicker)
        {
            if (ModelState.IsValid)
            {
                var model = new TeamLeavesType
                {
                    LeaveTypeName = leaveType,
                    DayValue = dayValue,
                    colorPicker = colorPicker
                };

                _context.TeamLeavesType.Add(model);
                await _context.SaveChangesAsync();


            }



            return RedirectToAction("EmployeeLeaveTracker"); // Re-display the form, potentially showing errors
        }



        [HttpGet]
        [Route("EmployeeData")]
        public IActionResult EmployeeData()
        {
            var locations = new[] { "Office", "Operation" };
            var employees = _context.Users.Where(u => locations.Contains(u.Located))
        .Select(u => new EmployeeViewModel
        {
            EmployeeId = u.EmployeeId,
            FirstName = u.FirstName,
            LastName = u.LastName,
            JoinDate = u.JoiningDate,
            ResignDate=u.ResignRejoinDate



        })
        .ToList();

            return View(employees);
        }

        [HttpGet]
        [Route("LeavesDashboard")]
        public IActionResult LeavesDashboard()
        {
            var locations = new[] { "Office", "Operation" };
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            var employees = _context.Users
.Where(u => u.EmployeeStatus != "Resign" && u.EmployeeId != null)
.OrderBy(u => u.EmployeeId)
.Select(u => new EmployeeViewModel
{
    EmployeeId = u.EmployeeId,
    FirstName = u.FirstName,
    LastName = u.LastName,
    JoinDate = u.JoiningDate,
})
.ToList();
            ViewBag.CurrentstringMonth = DateTime.Now.ToString("MMM");
            ViewBag.CurrentMonth = currentMonth;
            ViewBag.CurrentYear = currentYear;

            var employeeIds = employees.Select(e => e.EmployeeId).ToList();

            // Get all weekend mappings for those employees
            var teamMemberWeekendOffs = _context.TeamMemberWeekendOff
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .ToList();



            // Get distinct TeamWeekendOff IDs
            var teamWeekendOffIds = teamMemberWeekendOffs.Select(e => e.TeamWeekendOffId).Distinct().ToList();

            // Get TeamWeekendOff info (StartDate, EndDate, DayName)
            var teamWeekendOffs = _context.TeamWeekendOff
                .Where(t => teamWeekendOffIds.Contains(t.Id)) // assuming Id is the PK
                .Select(t => new
                {
                    t.Id,
                    t.Weekend,         // e.g., "Sunday"
                    t.StartDate,
                    t.EndDate,
                    t.WeekendDays
                })
                .ToList();

            // Create mapping for quick lookup
            var weekendMap = teamMemberWeekendOffs
                .Select(member => new
                {
                    member.EmployeeId,
                    WeekendInfo = teamWeekendOffs.FirstOrDefault(w => w.Id == member.TeamWeekendOffId)
                })
                .Where(x => x.WeekendInfo != null)
                .ToList();

            // Dictionary<EmployeeId, List<DateTime>> => Sundays or other weekend dates for each employee in the current month
            var employeeWeekendDates = new Dictionary<string, List<DateTime>>();

            foreach (var entry in weekendMap)
            {
                var empId = entry.EmployeeId;
                var weekendInfo = entry.WeekendInfo;

                List<DateTime> weekendDates = new();

                if (weekendInfo.StartDate == null && weekendInfo.EndDate == null)
                {
                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekends))
                    {
                        int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);





                        var occurrenceList = weekendInfo.WeekendDays
                            .Split(',')
                            .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                            .Where(n => n > 0)
                            .ToList();


                        int weekendDayCount = 0;

                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            var date = new DateTime(currentYear, currentMonth, day);

                            if (date.DayOfWeek == weekends)
                            {

                                if (weekendInfo.WeekendDays == "All")
                                {
                                    if (date.DayOfWeek == weekends)
                                    {
                                        weekendDates.Add(date);
                                    }
                                }
                                else
                                {
                                    if (date.DayOfWeek == weekends)
                                    {
                                        weekendDayCount++; // Track how many Saturdays/Sundays have occurred

                                        if (occurrenceList.Contains(weekendDayCount))
                                        {
                                            weekendDates.Add(date);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var date in weekendDates)
                        {
                            Console.WriteLine(date.ToString("yyyy-MM-dd"));
                        }
                    }
                }



                else
                {
                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                    {
                        if (weekendInfo.StartDate.HasValue && weekendInfo.EndDate.HasValue)
                        {
                            var startDate = weekendInfo.StartDate.Value;
                            var endDate = weekendInfo.EndDate.Value;

                            var occurrenceList = weekendInfo.WeekendDays
                           .Split(',')
                           .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                           .Where(n => n > 0)
                           .ToList();


                            int weekendDayCount = 0;
                            int MainMonth = startDate.Month;

                            for (var date = startDate; date <= endDate; date = date.AddDays(1))
                            {

                                if (date.Month != MainMonth)
                                {
                                    MainMonth = date.Month;
                                    weekendDayCount = 0;
                                }
                                if (date.Month == currentMonth)
                                {
                                    if (date.DayOfWeek == weekendDay)
                                    {
                                        if (weekendInfo.WeekendDays == "All")
                                        {
                                            weekendDates.Add(date);
                                        }
                                        else
                                        {
                                            weekendDayCount++;

                                            if (occurrenceList.Contains(weekendDayCount))
                                            {
                                                weekendDates.Add(date);
                                            }
                                        }
                                    }
                                }
                            }




                        }
                        else
                        {

                            Console.WriteLine("StartDate or EndDate is null.");
                        }
                    }
                }



                if (employeeWeekendDates.ContainsKey(empId))
                    employeeWeekendDates[empId].AddRange(weekendDates);
                else
                    employeeWeekendDates[empId] = weekendDates;
            }


            var operationTeamEmployeeIds = _context.Users
              .Where(e => e.Located == "Office")
              .Select(e => e.EmployeeId)
              .ToList();
            var EmployeeHolidays = _context.EmployeeHolidays.Select(t => new
            {
                t.Id,
                t.Date,         

            })
                .ToList();

    
            var operationEmployeeHolidayDates = new Dictionary<string, List<DateTime>>();



            if (EmployeeHolidays != null)
            {
                foreach (var empId in operationTeamEmployeeIds)
                {
                    if (empId != null)
                    {

                        operationEmployeeHolidayDates[empId] = EmployeeHolidays.Select(h => h.Date).ToList();
                    }
                }
            }


            var EmployeeLeaves = _context.EmployeeLeaves.Select(t => new
            {
                t.EmployeeId,
                t.StartDate,
                t.EndDate,
                t.Leave,// e.g., "Sunday"

            })
           .ToList();


            var leaveDatesDict = new Dictionary<string, List<(DateTime Date, string Leave, string LeaveType)>>();

            //var leaveDatesList = new Dictionary<(string,List<DateTime>)>();

            var employeeLeaves = _context.EmployeeLeaves
                .Where(t => t.StartDate.HasValue && t.EndDate.HasValue && t.StartDate.Value.Year == currentYear && t.HRApproval == "Approved")
                .Select(t => new
                {
                    t.EmployeeId,
                    t.StartDate,
                    t.EndDate,
                    t.Leave,
                    t.LeaveType,
                })
                .ToList();

            foreach (var leave in employeeLeaves)
            {
                var empId = leave.EmployeeId.ToString(); // ensure string key
                var startDate = leave.StartDate.Value;
                var endDate = leave.EndDate.Value;

                var empHolidayDates = operationEmployeeHolidayDates.ContainsKey(empId)
                    ? operationEmployeeHolidayDates[empId]
                    : new List<DateTime>();

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    // Skip dates that fall on a holiday
                    if (empHolidayDates.Any(h => h.Date == date.Date))
                        continue;

                    if (!leaveDatesDict.ContainsKey(empId))
                    {
                        leaveDatesDict[empId] = new List<(DateTime, string, string)>();
                    }

                    leaveDatesDict[empId].Add((date, leave.Leave, leave.LeaveType));
                }
            }
            var leaveCount = _context.TeamLeavesType
    .GroupBy(t => t.LeaveTypeName)
    .Select(g => new LeaveCountViewModel
    {
        LeaveType = g.Key,
        Count = g.Sum(t => t.DayValue),
        color = g.FirstOrDefault().colorPicker // assuming at least one item exists in each group
    })
    .ToList();

            var employeeRosterLeave = _context.EmployeeRosterLeave.ToList();

            Dictionary<string, int> unpaidLeaveBalance = new Dictionary<string, int>();

            var today = DateTime.Today;
            var previousMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            int prevMonth = previousMonth.Month;
            int prevYear = previousMonth.Year;

            foreach (var emp in employeeIds)
            {
                if (emp != null)
                {
                    if (leaveDatesDict.ContainsKey(emp))
                    {
                        var leaves = leaveDatesDict[emp];

                        // Filter leaves to only those from the previous month and are Unpaid
                        int unpaidCount = leaves.Count(l =>
                            l.Item3 == "Unpaid" &&
                            l.Item1.Month == prevMonth &&
                            l.Item1.Year == prevYear
                        );

                        int remaining = 30 - unpaidCount;

                        unpaidLeaveBalance[emp] = remaining;
                    }
                    else
                    {
                        // No leaves = full balance
                        unpaidLeaveBalance[emp] = 30;
                    }
                }
            }

            TempData["UnpaidLeaveBalance"] = JsonConvert.SerializeObject(unpaidLeaveBalance);

            ViewBag.WorkingDays = unpaidLeaveBalance;
            ViewBag.OperationleaveCount = leaveCount;
            ViewBag.OperationEmployeeLeaves = leaveDatesDict;
            ViewBag.employeeRosterLeaves = employeeRosterLeave;
            // Pass to view
            ViewBag.OperationEmployeeHolidays = operationEmployeeHolidayDates;

            ViewBag.EmployeeWeekendDates = employeeWeekendDates;
            return View(employees);
        }

        [HttpPost]
        [Route("MonthWiseLeavesDashboard")]
        public IActionResult MonthWiseLeavesDashboard(string selectedMonth)
        {
            var locations = new[] { "Office", "Operation" };
            var parts = selectedMonth.Split('-');
            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            var currentMonth = month;
            var currentYear = year;
            var employees = _context.Users
.Where(u => u.EmployeeStatus != "Resign" && u.EmployeeId != null)
.OrderBy(u => u.EmployeeId)
.Select(u => new EmployeeViewModel
{
  EmployeeId = u.EmployeeId,
  FirstName = u.FirstName,
  LastName = u.LastName,
  JoinDate = u.JoiningDate,
})
.ToList();
            string monthName = new DateTime(1, month, 1).ToString("MMM");
            ViewBag.CurrentstringMonth = monthName;
            ViewBag.CurrentMonth = currentMonth;
            ViewBag.CurrentYear = currentYear;

            var employeeIds = employees.Select(e => e.EmployeeId).ToList();

            // Get all weekend mappings for those employees
            var teamMemberWeekendOffs = _context.TeamMemberWeekendOff
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .ToList();

            // Get distinct TeamWeekendOff IDs
            var teamWeekendOffIds = teamMemberWeekendOffs.Select(e => e.TeamWeekendOffId).Distinct().ToList();

            // Get TeamWeekendOff info (StartDate, EndDate, DayName)
            var teamWeekendOffs = _context.TeamWeekendOff
                .Where(t => teamWeekendOffIds.Contains(t.Id)) // assuming Id is the PK
                .Select(t => new
                {
                    t.Id,
                    t.Weekend,         // e.g., "Sunday"
                    t.StartDate,
                    t.EndDate,
                    t.WeekendDays
                })
                .ToList();

            // Create mapping for quick lookup
            var weekendMap = teamMemberWeekendOffs
                .Select(member => new
                {
                    member.EmployeeId,
                    WeekendInfo = teamWeekendOffs.FirstOrDefault(w => w.Id == member.TeamWeekendOffId)
                })
                .Where(x => x.WeekendInfo != null)
                .ToList();

            // Dictionary<EmployeeId, List<DateTime>> => Sundays or other weekend dates for each employee in the current month
            var employeeWeekendDates = new Dictionary<string, List<DateTime>>();

            foreach (var entry in weekendMap)
            {
                var empId = entry.EmployeeId;
                var weekendInfo = entry.WeekendInfo;

                List<DateTime> weekendDates = new();

                if (weekendInfo.StartDate == null && weekendInfo.EndDate == null)
                {
                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekends))
                    {
                        int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);





                        var occurrenceList = weekendInfo.WeekendDays
                            .Split(',')
                            .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                            .Where(n => n > 0)
                            .ToList();


                        int weekendDayCount = 0;

                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            var date = new DateTime(currentYear, currentMonth, day);

                            if (date.DayOfWeek == weekends)
                            {

                                if (weekendInfo.WeekendDays == "All")
                                {
                                    if (date.DayOfWeek == weekends)
                                    {
                                        weekendDates.Add(date);
                                    }
                                }
                                else
                                {
                                    if (date.DayOfWeek == weekends)
                                    {
                                        weekendDayCount++; // Track how many Saturdays/Sundays have occurred

                                        if (occurrenceList.Contains(weekendDayCount))
                                        {
                                            weekendDates.Add(date);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var date in weekendDates)
                        {
                            Console.WriteLine(date.ToString("yyyy-MM-dd"));
                        }
                    }
                }



                else
                {
                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                    {
                        if (weekendInfo.StartDate.HasValue && weekendInfo.EndDate.HasValue)
                        {
                            var startDate = weekendInfo.StartDate.Value;
                            var endDate = weekendInfo.EndDate.Value;

                            var occurrenceList = weekendInfo.WeekendDays
                           .Split(',')
                           .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                           .Where(n => n > 0)
                           .ToList();


                            int weekendDayCount = 0;
                            int MainMonth = startDate.Month;

                            for (var date = startDate; date <= endDate; date = date.AddDays(1))
                            {

                                if (date.Month != MainMonth)
                                {
                                    MainMonth = date.Month;
                                    weekendDayCount = 0;
                                }
                                if (date.Month == currentMonth)
                                {
                                    if (date.DayOfWeek == weekendDay)
                                    {
                                        if (weekendInfo.WeekendDays == "All")
                                        {
                                            weekendDates.Add(date);
                                        }
                                        else
                                        {
                                            weekendDayCount++;

                                            if (occurrenceList.Contains(weekendDayCount))
                                            {
                                                weekendDates.Add(date);
                                            }
                                        }
                                    }
                                }
                            }




                        }
                        else
                        {

                            Console.WriteLine("StartDate or EndDate is null.");
                        }
                    }
                }



                if (employeeWeekendDates.ContainsKey(empId))
                    employeeWeekendDates[empId].AddRange(weekendDates);
                else
                    employeeWeekendDates[empId] = weekendDates;
            }


            var operationTeamEmployeeIds = _context.Users
             .Where(e => e.Located == "Office")
             .Select(e => e.EmployeeId)
             .ToList();
            var EmployeeHolidays = _context.EmployeeHolidays.Select(t => new
            {
                t.Id,
                t.Date,         // e.g., "Sunday"

            })
                .ToList();

            // Create dictionary to hold holiday dates mapped to each Operation employee
            var operationEmployeeHolidayDates = new Dictionary<string, List<DateTime>>();



            if (EmployeeHolidays != null)
            {
                foreach (var empId in operationTeamEmployeeIds)
                {
                    if (empId != null)
                    {

                        operationEmployeeHolidayDates[empId] = EmployeeHolidays.Select(h => h.Date).ToList();
                    }
                }
            }

            var leaveDatesDict = new Dictionary<string, List<(DateTime Date, string Leave, string LeaveType)>>();

            // Calculate first and last day of the selected month for overlap filtering
            var firstDayOfMonth = new DateTime(currentYear, currentMonth, 1);
            var lastDayOfMonth = new DateTime(currentYear, currentMonth, DateTime.DaysInMonth(currentYear, currentMonth));

            var employeeLeaves = _context.EmployeeLeaves
                .Where(t => t.StartDate.HasValue && t.EndDate.HasValue
                    && t.EndDate.Value >= firstDayOfMonth   // leave ends on or after the 1st of selected month
                    && t.StartDate.Value <= lastDayOfMonth  // leave starts on or before the last day of selected month
                    && t.HRApproval == "Approved")
                .Select(t => new
                {
                    t.EmployeeId,
                    t.StartDate,
                    t.EndDate,
                    t.Leave,
                    t.LeaveType,
                })
                .ToList();

            foreach (var leave in employeeLeaves)
            {
                var empId = leave.EmployeeId.ToString(); // ensure string key
                var startDate = leave.StartDate.Value;
                var endDate = leave.EndDate.Value;

                var empHolidayDates = operationEmployeeHolidayDates.ContainsKey(empId)
                    ? operationEmployeeHolidayDates[empId]
                    : new List<DateTime>();

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    // Only include dates that fall within the selected month
                    if (date.Year != currentYear || date.Month != currentMonth)
                        continue;

                    // Skip dates that fall on a holiday
                    if (empHolidayDates.Any(h => h.Date == date.Date))
                        continue;

                    if (!leaveDatesDict.ContainsKey(empId))
                    {
                        leaveDatesDict[empId] = new List<(DateTime, string, string)>();
                    }

                    leaveDatesDict[empId].Add((date, leave.Leave, leave.LeaveType));
                }
            }
            var leaveCount = _context.TeamLeavesType
    .GroupBy(t => t.LeaveTypeName)
    .Select(g => new LeaveCountViewModel
    {
        LeaveType = g.Key,
        Count = g.Sum(t => t.DayValue),
        color = g.FirstOrDefault().colorPicker // assuming at least one item exists in each group
    })
    .ToList();


            var employeeRosterLeave = _context.EmployeeRosterLeave.ToList();


            ViewBag.OperationleaveCount = leaveCount;
            ViewBag.OperationEmployeeLeaves = leaveDatesDict;
            ViewBag.employeeRosterLeaves = employeeRosterLeave;
            ViewBag.OperationEmployeeHolidays = operationEmployeeHolidayDates;
            ViewBag.EmployeeWeekendDates = employeeWeekendDates;
            return View(employees);
        }


        [HttpGet]
        [Route("EmployeeReport")]
        public IActionResult EmployeeReport()
        {
          
            var employees = _context.Users
.Where(u =>u.EmployeeStatus!= "Resign" && u.EmployeeId!=null)
.OrderBy(u => u.EmployeeId) 
.Select(u => new EmployeeViewModel
{
    EmployeeId = u.EmployeeId,
    FirstName = u.FirstName,
    LastName = u.LastName,
    JoinDate = u.JoiningDate,
})
.ToList();



            var employeeData = _context.Users
 .Where(u => u.EmployeeId != null)
  .OrderBy(u => u.EmployeeId)
 .Select(u => new
 {
     u.EmployeeId,
     u.JoiningDate,
     u.Located
 })
 .FirstOrDefault();
            var currentYear = DateTime.Now.Year;
            int LeavesDays = 0;

            if (employeeData.JoiningDate != null)
            {


                if (employeeData.JoiningDate.Value.Year == currentYear)
                {
                    var joiningDate = employeeData.JoiningDate.Value;


                    // End of year
                    var endOfYear = new DateTime(currentYear, 12, 31);

                    // ⿡ Get total whole months left after the joining month
                    int fullMonthsLeft = 12 - joiningDate.Month;

                    // ⿢ Calculate partial month for the joining month
                    int daysInJoiningMonth = DateTime.DaysInMonth(joiningDate.Year, joiningDate.Month);
                    int remainingDaysInJoiningMonth = daysInJoiningMonth - joiningDate.Day + 1;

                    // partial month fraction
                    double partialMonth = (double)remainingDaysInJoiningMonth / daysInJoiningMonth;

                    // ⿣ Add them up
                    double totalMonths = fullMonthsLeft + partialMonth;
                    int LeaveMonth = 0;
                    // Round to 2 decimal places for clarity
                    LeaveMonth = (int)Math.Round(totalMonths);

                    Console.WriteLine($"Total months remaining this year: {totalMonths}");

                    double leavePerMonth = 1.833;
                    double totalLeave = totalMonths * leavePerMonth;

                    // Round off to 2 decimal places
                    LeavesDays = (int)Math.Round(totalLeave);
                }
            }
            else
            {
                if (employeeData.Located == "Office")
                {
                    LeavesDays = _context.LeavesForOperationAndOffice.Select(s => s.OfficeValue).FirstOrDefault();
                }
                else
                {
                    LeavesDays = _context.LeavesForOperationAndOffice.Select(s => s.OperationValue).FirstOrDefault();
                }

            }
            int year = DateTime.Now.Year;
            // Get all weekend mappings for those employees
            var teamMemberWeekendOffs = _context.TeamMemberWeekendOff
     .Where(e => e.EmployeeId == employeeData.EmployeeId)
     .ToList();

            // Get distinct TeamWeekendOff IDs
            var teamWeekendOffIds = teamMemberWeekendOffs.Select(e => e.TeamWeekendOffId).Distinct().ToList();

            // Get TeamWeekendOff info (StartDate, EndDate, DayName)
            var teamWeekendOffs = _context.TeamWeekendOff
                .Where(t => teamWeekendOffIds.Contains(t.Id)) // assuming Id is the PK
                .Select(t => new
                {
                    t.Id,
                    t.Weekend,         // e.g., "Sunday"
                    t.StartDate,
                    t.EndDate,
                    t.WeekendDays
                })
                .ToList();

            // Create mapping for quick lookup
            var weekendMap = teamMemberWeekendOffs
                .Select(member => new
                {
                    member.EmployeeId,
                    WeekendInfo = teamWeekendOffs.FirstOrDefault(w => w.Id == member.TeamWeekendOffId)
                })
                .Where(x => x.WeekendInfo != null)
                .ToList();

            // Dictionary<EmployeeId, List<DateTime>> => Sundays or other weekend dates for each employee in the current month
            var employeeWeekendDates = new Dictionary<string, List<DateTime>>();

            foreach (var entry in weekendMap)
            {
                var empId = entry.EmployeeId;
                var weekendInfo = entry.WeekendInfo;

                List<DateTime> weekendDates = new();


                if (weekendInfo.StartDate == null && weekendInfo.EndDate == null)
                {
                    // Get day of week (e.g., Sunday, Saturday)


                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekends))
                    {






                        var occurrenceList = weekendInfo.WeekendDays
                            .Split(',')
                            .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                            .Where(n => n > 0)
                            .ToList();


                      

                        //for (int month = 1; month <= 12; month++)
                        //{
                        //    int daysInMonth = DateTime.DaysInMonth(year, month);

                            for (int month = 1; month <= 12; month++) // loop over months
                            {
                                int weekendDayCount = 0; // Reset at the beginning of each month
                                int daysInMonth = DateTime.DaysInMonth(year, month);

                                for (int day = 1; day <= daysInMonth; day++)
                                {
                                    var date = new DateTime(year, month, day);

                                    if (date.DayOfWeek == weekends)
                                    {
                                        weekendDayCount++; // Count only the matching weekend days (e.g., Sundays)

                                        if (weekendInfo.WeekendDays == "All")
                                        {
                                            weekendDates.Add(date);
                                        }
                                        else
                                        {
                                            if (occurrenceList.Contains(weekendDayCount))
                                            {
                                                weekendDates.Add(date);
                                            }
                                        }
                                    }
                                }
                            

                            foreach (var date in weekendDates)
                            {
                                Console.WriteLine(date.ToString("yyyy-MM-dd"));
                            }
                        }
                        //another




                        //    for (int month = 1; month <= 12; month++)
                        //    {
                        //        int daysInMonth = DateTime.DaysInMonth(year, month);

                        //        for (int day = 1; day <= daysInMonth; day++)
                        //        {
                        //            var date = new DateTime(year, month, day);
                        //            if (date.DayOfWeek == weekendDay)
                        //            {
                        //                weekendDates.Add(date);
                        //            }
                        //        }
                        //    }


                        //    foreach (var date in weekendDates)
                        //    {
                        //        Console.WriteLine(date.ToString("yyyy-MM-dd")); // Or use date as needed
                        //    }
                    }
                }


                else
                {

                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                    {
                        if (weekendInfo.StartDate.HasValue && weekendInfo.EndDate.HasValue)
                        {
                            var startDate = weekendInfo.StartDate.Value;
                            var endDate = weekendInfo.EndDate.Value;

                            var occurrenceList = weekendInfo.WeekendDays
                                .Split(',')
                                .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                                .Where(n => n > 0)
                                .ToList();

                            int weekendDayCount = 0;
                            int MainMonth = startDate.Month;

                            for (var date = startDate; date <= endDate; date = date.AddDays(1))
                            {
                                // ✅ Reset weekendDayCount when the month changes
                                if (date.Month != MainMonth)
                                {
                                    MainMonth = date.Month;
                                    weekendDayCount = 0;
                                }

                                // ✅ Check for the specific weekend day (e.g., Saturday or Sunday)
                                if (date.DayOfWeek == weekendDay)
                                {
                                    if (weekendInfo.WeekendDays == "All")
                                    {
                                        weekendDates.Add(date); // Add all matching weekend days
                                    }
                                    else
                                    {
                                        weekendDayCount++;

                                        if (occurrenceList.Contains(weekendDayCount))
                                        {
                                            weekendDates.Add(date); // Add only matching occurrences
                                        }
                                    }
                                }
                            }
                        }

                        else
                        {

                            Console.WriteLine("StartDate or EndDate is null.");
                        }
                    }


                    //another
                    //if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                    //{
                    //    if (weekendInfo.StartDate.HasValue && weekendInfo.EndDate.HasValue)
                    //    {
                    //        var startDate = weekendInfo.StartDate.Value;
                    //        var endDate = weekendInfo.EndDate.Value;



                    //        for (var date = startDate; date <= endDate; date = date.AddDays(1))
                    //        {
                    //            if (date.DayOfWeek == weekendDay)
                    //            {
                    //                weekendDates.Add(date);
                    //            }
                    //        }

                    //        foreach (var date in weekendDates)
                    //        {
                    //            Console.WriteLine(date.ToString("yyyy-MM-dd"));
                    //        }
                    //    }
                    //    else
                    //    {
                    //        // Handle case when StartDate or EndDate is null
                    //        Console.WriteLine("StartDate or EndDate is null.");
                    //    }
                    //}
                }



                if (employeeWeekendDates.ContainsKey(empId))
                    employeeWeekendDates[empId].AddRange(weekendDates);
                else
                    employeeWeekendDates[empId] = weekendDates;
            }


            var employeeLocation = _context.Users
    .Where(e => e.EmployeeId == employeeData.EmployeeId)
    .Select(e => e.Located)
    .FirstOrDefault(); // Single string value

            // Get all holidays
            var employeeHolidays = _context.EmployeeHolidays.Where(t=>t.Date.Year==currentYear)
                .Select(t => new
                {
                    t.Id,
                    t.Date
                })
                .ToList();

            // Prepare the dictionary
            var operationEmployeeHolidayDates = new Dictionary<string, List<DateTime>>();

            // If employee is in Operation team
            if (employeeHolidays != null && employeeLocation == "Office")
            {
                operationEmployeeHolidayDates[employeeData.EmployeeId] = employeeHolidays.Select(h => h.Date).ToList();
            }

            var leaveDatesDict = new Dictionary<string, List<(DateTime Date, string Leave, string LeaveType)>>();

            var employeeLeaves = _context.EmployeeLeaves
               .Where(t => t.StartDate.HasValue && t.EndDate.HasValue && t.EmployeeId == employeeData.EmployeeId && t.StartDate.Value.Year == currentYear && t.HRApproval == "Approved")
               .Select(t => new
               {
                   t.EmployeeId,
                   t.StartDate,
                   t.EndDate,
                   t.Leave,
                   t.LeaveType,
               })
               .ToList();

            foreach (var leave in employeeLeaves)
            {
                var empId = leave.EmployeeId.ToString(); // ensure string key
                var startDate = leave.StartDate.Value;
                var endDate = leave.EndDate.Value;

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (!leaveDatesDict.ContainsKey(empId))
                    {
                        leaveDatesDict[empId] = new List<(DateTime, string, string)>();
                    }

                    leaveDatesDict[empId].Add((date, leave.Leave, leave.LeaveType));
                }
            }




            var leaveCount = _context.TeamLeavesType
    .GroupBy(t => t.LeaveTypeName)
    .Select(g => new LeaveCountViewModel
    {
        LeaveType = g.Key,
        Count = g.Sum(t => t.DayValue),
        color = g.FirstOrDefault().colorPicker // assuming at least one item exists in each group
    })
    .ToList();


            var employeeRosterLeave = _context.EmployeeRosterLeave.ToList();



            ViewBag.OperationleaveCount = leaveCount;
            ViewBag.year = year;
            ViewBag.OperationEmployeeLeaves = leaveDatesDict;
            ViewBag.employeeRosterLeaves = employeeRosterLeave;
            // Pass to view
            ViewBag.OperationEmployeeHolidays = operationEmployeeHolidayDates;
            ViewBag.EmployeeWeekendDates = employeeWeekendDates;
            ViewBag.LeavesDays = LeavesDays;
            return View(employees);
        }



        [HttpPost]
        [Route("GetEmployeeLeaveData")]
        public IActionResult GetEmployeeLeaveData(string employeeId, int year)
        {
            var employees = _context.Users
.Where(u => u.EmployeeStatus != "Resign" && u.EmployeeId != null)
.OrderBy(u => u.EmployeeId)
.Select(u => new EmployeeViewModel
{
    EmployeeId = u.EmployeeId,
    FirstName = u.FirstName,
    LastName = u.LastName,
    JoinDate = u.JoiningDate,
})
.ToList();
            var employeeData = _context.Users
  .Where(u => u.EmployeeId ==employeeId)
   .OrderBy(u => u.EmployeeId)
  .Select(u => new
  {
      u.EmployeeId,
      u.JoiningDate,
      u.Located
  })
  .FirstOrDefault();

            var currentYear = DateTime.Now.Year;
            int LeavesDays = 0;

            if (employeeData.JoiningDate != null)
            {


                if (employeeData.JoiningDate.Value.Year == currentYear)
                {
                    var joiningDate = employeeData.JoiningDate.Value;


                    // End of year
                    var endOfYear = new DateTime(currentYear, 12, 31);

                    // ⿡ Get total whole months left after the joining month
                    int fullMonthsLeft = 12 - joiningDate.Month;

                    // ⿢ Calculate partial month for the joining month
                    int daysInJoiningMonth = DateTime.DaysInMonth(joiningDate.Year, joiningDate.Month);
                    int remainingDaysInJoiningMonth = daysInJoiningMonth - joiningDate.Day + 1;

                    // partial month fraction
                    double partialMonth = (double)remainingDaysInJoiningMonth / daysInJoiningMonth;

                    // ⿣ Add them up
                    double totalMonths = fullMonthsLeft + partialMonth;
                    int LeaveMonth = 0;
                    // Round to 2 decimal places for clarity
                    LeaveMonth = (int)Math.Round(totalMonths);

                    Console.WriteLine($"Total months remaining this year: {totalMonths}");

                    double leavePerMonth = 1.833;
                    double totalLeave = totalMonths * leavePerMonth;

                    // Round off to 2 decimal places
                    LeavesDays = (int)Math.Round(totalLeave);
                }
                else
                {
                    // Employee joined in a PREVIOUS year → give full leave entitlement
                    if (employeeData.Located == "Office")
                    {
                        LeavesDays = _context.LeavesForOperationAndOffice.Select(s => s.OfficeValue).FirstOrDefault();
                    }
                    else
                    {
                        LeavesDays = _context.LeavesForOperationAndOffice.Select(s => s.OperationValue).FirstOrDefault();
                    }
                }
            }
            else
            {
                // JoiningDate is null → give full leave entitlement
                if (employeeData.Located == "Office")
                {
                    LeavesDays = _context.LeavesForOperationAndOffice.Select(s => s.OfficeValue).FirstOrDefault();
                }
                else
                {
                    LeavesDays = _context.LeavesForOperationAndOffice.Select(s => s.OperationValue).FirstOrDefault();
                }

            }

            // Get all weekend mappings for those employees
            var teamMemberWeekendOffs = _context.TeamMemberWeekendOff
     .Where(e => e.EmployeeId == employeeId)
     .ToList();

            // Get distinct TeamWeekendOff IDs
            var teamWeekendOffIds = teamMemberWeekendOffs.Select(e => e.TeamWeekendOffId).Distinct().ToList();

            // Get TeamWeekendOff info (StartDate, EndDate, DayName)
            var teamWeekendOffs = _context.TeamWeekendOff
                .Where(t => teamWeekendOffIds.Contains(t.Id)) // assuming Id is the PK
                .Select(t => new
                {
                    t.Id,
                    t.Weekend,         // e.g., "Sunday"
                    t.StartDate,
                    t.EndDate,
                    t.WeekendDays
                })
                .ToList();

            // Create mapping for quick lookup
            var weekendMap = teamMemberWeekendOffs
                .Select(member => new
                {
                    member.EmployeeId,
                    WeekendInfo = teamWeekendOffs.FirstOrDefault(w => w.Id == member.TeamWeekendOffId)
                })
                .Where(x => x.WeekendInfo != null)
                .ToList();

            // Dictionary<EmployeeId, List<DateTime>> => Sundays or other weekend dates for each employee in the current month
            var employeeWeekendDates = new Dictionary<string, List<DateTime>>();

            foreach (var entry in weekendMap)
            {
                var empId = entry.EmployeeId;
                var weekendInfo = entry.WeekendInfo;

                List<DateTime> weekendDates = new();
                if (weekendInfo.StartDate == null && weekendInfo.EndDate == null)
                {
                    // Get day of week (e.g., Sunday, Saturday)


                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekends))
                    {






                        var occurrenceList = weekendInfo.WeekendDays
                            .Split(',')
                            .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                            .Where(n => n > 0)
                            .ToList();




                        //for (int month = 1; month <= 12; month++)
                        //{
                        //    int daysInMonth = DateTime.DaysInMonth(year, month);

                        for (int month = 1; month <= 12; month++) // loop over months
                        {
                            int weekendDayCount = 0; // Reset at the beginning of each month
                            int daysInMonth = DateTime.DaysInMonth(year, month);

                            for (int day = 1; day <= daysInMonth; day++)
                            {
                                var date = new DateTime(year, month, day);

                                if (date.DayOfWeek == weekends)
                                {
                                    weekendDayCount++; // Count only the matching weekend days (e.g., Sundays)

                                    if (weekendInfo.WeekendDays == "All")
                                    {
                                        weekendDates.Add(date);
                                    }
                                    else
                                    {
                                        if (occurrenceList.Contains(weekendDayCount))
                                        {
                                            weekendDates.Add(date);
                                        }
                                    }
                                }
                            }


                            foreach (var date in weekendDates)
                            {
                                Console.WriteLine(date.ToString("yyyy-MM-dd"));
                            }
                        }
                        //another




                        //    for (int month = 1; month <= 12; month++)
                        //    {
                        //        int daysInMonth = DateTime.DaysInMonth(year, month);

                        //        for (int day = 1; day <= daysInMonth; day++)
                        //        {
                        //            var date = new DateTime(year, month, day);
                        //            if (date.DayOfWeek == weekendDay)
                        //            {
                        //                weekendDates.Add(date);
                        //            }
                        //        }
                        //    }


                        //    foreach (var date in weekendDates)
                        //    {
                        //        Console.WriteLine(date.ToString("yyyy-MM-dd")); // Or use date as needed
                        //    }
                    }
                }


                else
                {

                    if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                    {
                        if (weekendInfo.StartDate.HasValue && weekendInfo.EndDate.HasValue)
                        {
                            var startDate = weekendInfo.StartDate.Value;
                            var endDate = weekendInfo.EndDate.Value;

                            var occurrenceList = weekendInfo.WeekendDays
                                .Split(',')
                                .Select(s => int.TryParse(s.Trim(), out int num) ? num : -1)
                                .Where(n => n > 0)
                                .ToList();

                            int weekendDayCount = 0;
                            int MainMonth = startDate.Month;

                            for (var date = startDate; date <= endDate; date = date.AddDays(1))
                            {
                                // ✅ Reset weekendDayCount when the month changes
                                if (date.Month != MainMonth)
                                {
                                    MainMonth = date.Month;
                                    weekendDayCount = 0;
                                }

                                // ✅ Check for the specific weekend day (e.g., Saturday or Sunday)
                                if (date.DayOfWeek == weekendDay)
                                {
                                    if (weekendInfo.WeekendDays == "All")
                                    {
                                        weekendDates.Add(date); // Add all matching weekend days
                                    }
                                    else
                                    {
                                        weekendDayCount++;

                                        if (occurrenceList.Contains(weekendDayCount))
                                        {
                                            weekendDates.Add(date); // Add only matching occurrences
                                        }
                                    }
                                }
                            }
                        }

                        else
                        {

                            Console.WriteLine("StartDate or EndDate is null.");
                        }
                    }


                    //another
                    //if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                    //{
                    //    if (weekendInfo.StartDate.HasValue && weekendInfo.EndDate.HasValue)
                    //    {
                    //        var startDate = weekendInfo.StartDate.Value;
                    //        var endDate = weekendInfo.EndDate.Value;



                    //        for (var date = startDate; date <= endDate; date = date.AddDays(1))
                    //        {
                    //            if (date.DayOfWeek == weekendDay)
                    //            {
                    //                weekendDates.Add(date);
                    //            }
                    //        }

                    //        foreach (var date in weekendDates)
                    //        {
                    //            Console.WriteLine(date.ToString("yyyy-MM-dd"));
                    //        }
                    //    }
                    //    else
                    //    {
                    //        // Handle case when StartDate or EndDate is null
                    //        Console.WriteLine("StartDate or EndDate is null.");
                    //    }
                    //}
                }



                if (employeeWeekendDates.ContainsKey(empId))
                    employeeWeekendDates[empId].AddRange(weekendDates);
                else
                    employeeWeekendDates[empId] = weekendDates;
            }










            var employeeLocation = _context.Users
    .Where(e => e.EmployeeId == employeeId)
    .Select(e => e.Located)
    .FirstOrDefault(); // Single string value

            // Get all holidays
            var employeeHolidays = _context.EmployeeHolidays.Where(t=>t.Date.Year==year)
                .Select(t => new
                {
                    t.Id,
                    t.Date
                })
                .ToList();

            // Prepare the dictionary
            var operationEmployeeHolidayDates = new Dictionary<string, List<DateTime>>();

            // If employee is in Operation team
            if (employeeHolidays != null && employeeLocation == "Office")
            {
                operationEmployeeHolidayDates[employeeId] = employeeHolidays.Select(h => h.Date).ToList();
            }

            // Build a HashSet of holiday dates for fast lookup
            var holidayDateSet = new HashSet<DateTime>(
                employeeHolidays?.Select(h => h.Date.Date) ?? Enumerable.Empty<DateTime>()
            );

            var leaveDatesDict = new Dictionary<string, List<(DateTime Date, string Leave, string LeaveType)>>();







            var employeeLeaves = _context.EmployeeLeaves
          .Where(t => t.StartDate.HasValue && t.EndDate.HasValue && t.EmployeeId == employeeId && t.StartDate.Value.Year == year && t.HRApproval == "Approved")
          .Select(t => new
          {
              t.EmployeeId,
              t.StartDate,
              t.EndDate,
              t.Leave,
              t.LeaveType,
          })
          .ToList();

            foreach (var leave in employeeLeaves)
            {
                var empId = leave.EmployeeId.ToString(); // ensure string key
                var startDate = leave.StartDate.Value;
                var endDate = leave.EndDate.Value;

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    // Skip if this date is a holiday
                    if (holidayDateSet.Contains(date.Date))
                        continue;

                    if (!leaveDatesDict.ContainsKey(empId))
                    {
                        leaveDatesDict[empId] = new List<(DateTime, string, string)>();
                    }

                    leaveDatesDict[empId].Add((date, leave.Leave, leave.LeaveType));
                }
            }


            var leaveCount = _context.TeamLeavesType
    .GroupBy(t => t.LeaveTypeName)
    .Select(g => new LeaveCountViewModel
    {
        LeaveType = g.Key,
        Count = g.Sum(t => t.DayValue),
        color = g.FirstOrDefault().colorPicker // assuming at least one item exists in each group
    })
    .ToList();
            var employeefirstName = _context.Users.Where(e => e.EmployeeId == employeeId).Select(e => e.FirstName).FirstOrDefault();
            var employeelastName = _context.Users.Where(e => e.EmployeeId == employeeId).Select(e => e.LastName).FirstOrDefault();
            var employeeRosterLeave = _context.EmployeeRosterLeave.Where(e => e.EmployeeId == employeeId).ToList();

            ViewBag.employeeRosterLeaves = employeeRosterLeave;
            ViewBag.OperationEmployeeLeaves = leaveDatesDict;
            ViewBag.OperationleaveCount = leaveCount;
            ViewBag.OperationEmployeeHolidays = operationEmployeeHolidayDates;
            ViewBag.EmployeeWeekendDates = employeeWeekendDates;
            ViewBag.employeeId = employeeId;
            ViewBag.FirstName = employeefirstName;
            ViewBag.LastName = employeelastName;
            ViewBag.year = year;
            ViewBag.LeavesDays = LeavesDays;
            return View(employees);
        }
        [HttpGet]
        [Route("EmployeeLeaves")]
        public IActionResult EmployeeLeaves()
        {
            var locations = new[] { "Office", "Operation" };

            var employees = _context.Users
                .Where(u => locations.Contains(u.Located))
                .Select(u => new EmployeeViewModel
                {
                    EmployeeId = u.EmployeeId,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    JoinDate = u.JoiningDate
                })
                .ToList();

            var leaves = _context.EmployeeLeaves
                        .OrderByDescending(e => e.Id) // or LeaveId
                        .ToList();// Fetch all leave records

            var leaveCount = _context.TeamLeavesType
  .GroupBy(t => t.LeaveTypeName)
  .Select(g => new LeaveCountViewModel
  {
      LeaveType = g.Key,
      Count = g.Sum(t => t.DayValue),
      color = g.FirstOrDefault().colorPicker // assuming at least one item exists in each group
  })
  .ToList();


            ViewBag.OperationleaveCount = leaveCount;

            var viewModel = new EmployeeViewModel
            {
                Employees = employees,
                Leaves = leaves
            };

            return View(viewModel);
        }


        [HttpPost]
        [Route("DeleteEmployeeLeave/{id}")]
        public async Task<IActionResult> DeleteEmployeeLeave(int id)
        {
            var leave = await _context.EmployeeLeaves.FindAsync(id);

            if (leave == null)
            {
                return NotFound(new { message = $"Leave with ID {id} not found." });
            }

            _context.EmployeeLeaves.Remove(leave);
            await _context.SaveChangesAsync();

            return Ok();
        }



        [HttpPost]
        [Route("UpdateEmployeeLeave")]
        public async Task<IActionResult> UpdateEmployeeLeave([FromBody] EmployeeLeaveModel model)
        {
            if (ModelState.IsValid)
            {
                var existingLeave = await _context.EmployeeLeaves.FindAsync(model.Id);
                if (existingLeave != null)
                {

                    existingLeave.EmployeeId = model.EmployeeId;
                    existingLeave.FirstName = model.FullName.Split(' ')[0];
                    existingLeave.LastName = model.FullName.Split(' ').Length > 1 ? model.FullName.Split(' ')[1] : "";
                    existingLeave.StartDate = DateTime.Parse(model.StartDate);
                    existingLeave.EndDate = DateTime.Parse(model.EndDate);
                    existingLeave.LeaveType = model.LeaveType;
                    existingLeave.Leave = model.Leave;
                  

                    _context.Update(existingLeave);
                    await _context.SaveChangesAsync();
                    return Ok();
                }
                return NotFound();
            }

            return BadRequest();
        }

        [HttpPost]
        [Route("AddEmployeeLeaves")]
        public IActionResult AddEmployeeLeaves(string employeeData, DateTime startDate, DateTime endDate, string leaveType, string leave, string note)
        {
            var employee = _context.Users
    .Where(p => p.EmployeeId == employeeData)
   
    .FirstOrDefault();

            string firstName = employee.FirstName;
            string lastName = employee.LastName;
            string position = employee.Position;
            string location = employee.Located;



            EmployeeLeaves savedLeave = null;
           
                string approvalStatus = string.Empty;
            if (leaveType.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
   leaveType.Equals("Other", StringComparison.OrdinalIgnoreCase))
                approvalStatus = "Approved";
                else if (leaveType.Equals("Unpaid", StringComparison.OrdinalIgnoreCase))
                    approvalStatus = "Rejected";
               
                var model = new EmployeeLeaves
                {
                    FirstName = firstName,
                    LastName = lastName,
                    EmployeeId = employeeData,
                    StartDate = startDate,
                    EndDate = endDate,
                    Leave = leave,
                    LeaveType = leaveType,
                    Note = note,
                    Approval = approvalStatus,
                    HRApproval=approvalStatus,
                    ApprovedBy="HR",
                    Position=position,
                    Located=location

                };


                

                _context.EmployeeLeaves.Add(model);
                _context.SaveChanges();

                TempData["LeaveId"] = model.Id;
            
            return RedirectToAction("EmployeeLeaves");
        }

        [HttpGet]
        [Route("AddWeekendOff")]
        public IActionResult AddWeekendOff()
        {
            var Officeemployees = _context.Users
               .Where(u => u.Located == "Office")
               .Select(u => new EmployeeViewModel
               {
                   EmployeeId = u.EmployeeId,
                   FirstName = u.FirstName,
                   LastName = u.LastName,
                   JoinDate = u.JoiningDate
               })
               .ToList();

            var Operationemployees = _context.Users
              .Where(u => u.Located == "Operation")
              .Select(u => new EmployeeViewModel
              {
                  EmployeeId = u.EmployeeId,
                  FirstName = u.FirstName,
                  LastName = u.LastName,
                  JoinDate = u.JoiningDate
              })
              .ToList();

            var teamLeaveData = _context.TeamWeekendOff
         .Include(t => t.TeamMemberWeekendOff)
         .ToList();

            var viewModel = new EmployeeViewModel
            {
                Employees = Officeemployees,
                OperationEmployees = Operationemployees,
                TeamWeekendLeaves = teamLeaveData
            };

            return View(viewModel);
        }

        [HttpPost]
        [Route("AddEmployeeWeekendLeaves")]
        public async Task<IActionResult> AddEmployeeWeekendLeaves(
 string selectedDays,
 string seasoneOrYearData,
 string seasoneData,
 List<string> employeeData,
 List<string> days,
 DateTime? startDate,
 DateTime? endDate)
        {
            if (string.IsNullOrEmpty(selectedDays) || employeeData == null || !employeeData.Any())
            {
                return BadRequest("Invalid input data");
            }

            if (seasoneData == null)
            {


                var teamSesoneOff = new TeamWeekendOff
                {
                    Weekend = selectedDays,
                    SeasoneOrYear = seasoneOrYearData,
                    TeamMemberWeekendOff = new List<TeamMemberWeekendOff>()
                };


                foreach (var entry in employeeData)
                {
                    var parts = entry.Split('|');
                    if (parts.Length < 1)
                    {
                        return BadRequest("Employee data format is invalid.");
                    }

                    var empId = parts[0];
                    var user = _context.Users
                        .Where(t => t.EmployeeId == empId)
                        .Select(t => new { t.FirstName, t.LastName })
                        .FirstOrDefault();

                    var member = new TeamMemberWeekendOff
                    {
                        EmployeeId = empId,
                        FirstName = user?.FirstName,
                        LastName = user?.LastName,
                        Weekend = selectedDays
                    };

                    teamSesoneOff.TeamMemberWeekendOff.Add(member);
                }

                foreach (var data in days)
                {
                    var daysparts = data.Split('|');
                    if (daysparts.Length < 1)
                    {
                        return BadRequest("Weekend days data format is invalid.");
                    }

                    var weekendDays = daysparts[0];
                    // You can append or override based on your logic
                    teamSesoneOff.WeekendDays += string.IsNullOrEmpty(teamSesoneOff.WeekendDays)
                        ? weekendDays
                        : $",{weekendDays}";
                }
                _context.TeamWeekendOff.Add(teamSesoneOff);
            }
            else
            {
                if (startDate == null || endDate == null)
                {
                    return BadRequest("Start and End dates are required for seasonal data.");
                }

                var teamWeekendOff = new TeamWeekendOff
                {
                    Weekend = selectedDays,
                    SeasoneOrYear = seasoneOrYearData,
                    Seasone = seasoneData,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    TeamMemberWeekendOff = new List<TeamMemberWeekendOff>()
                };

                foreach (var entry in employeeData)
                {
                    var parts = entry.Split('|');
                    if (parts.Length < 1)
                    {
                        return BadRequest("Employee data format is invalid.");
                    }

                    var empId = parts[0];
                    var user = _context.Users
                        .Where(t => t.EmployeeId == empId)
                        .Select(t => new { t.FirstName, t.LastName })
                        .FirstOrDefault();

                    var member = new TeamMemberWeekendOff
                    {
                        EmployeeId = empId,
                        FirstName = user?.FirstName,
                        LastName = user?.LastName,
                        Weekend = selectedDays
                    };

                    teamWeekendOff.TeamMemberWeekendOff.Add(member);
                }
                foreach (var data in days)
                {
                    var daysparts = data.Split('|');
                    if (daysparts.Length < 1)
                    {
                        return BadRequest("Weekend days data format is invalid.");
                    }

                    var weekendDays = daysparts[0];
                    // You can append or override based on your logic
                    teamWeekendOff.WeekendDays += string.IsNullOrEmpty(teamWeekendOff.WeekendDays)
                        ? weekendDays
                        : $",{weekendDays}";
                }
                _context.TeamWeekendOff.Add(teamWeekendOff);
            }

            await _context.SaveChangesAsync();

            return Redirect("AddWeekendOff");
        }


        [HttpPost]
        [Route("RemoveEmployeeFromWeekendOff/{teamWeekendOffId}/{employeeId}")]
        public IActionResult RemoveEmployeeFromWeekendOff(int teamWeekendOffId, string employeeId)
        {
            var member = _context.TeamMemberWeekendOff
                 .FirstOrDefault(x => x.EmployeeId == employeeId && x.Id == teamWeekendOffId);

            var teamId = member.TeamWeekendOffId;



            if (member != null)
            {
                _context.TeamMemberWeekendOff.Remove(member);
                _context.SaveChanges();
            }

            // Check if other members exist under same TeamWeekendOffId
            var hasOtherMembers = _context.TeamMemberWeekendOff
                .Any(t => t.TeamWeekendOffId == teamId);

            if (!hasOtherMembers)
            {
                var teamWeekendOff = _context.TeamWeekendOff
                    .FirstOrDefault(t => t.Id == teamId);

                if (teamWeekendOff != null)
                {
                    _context.TeamWeekendOff.Remove(teamWeekendOff);
                    _context.SaveChanges();
                }
            }

            return Ok();
        }

        [HttpGet]
        [Route("EmployeeRosterDayOff")]
        public IActionResult EmployeeRosterDayOff()
        {
            var locations = new[] { "Office", "Operation" };

            var employees = _context.Users
                .Where(u => locations.Contains(u.Located))
                .Select(u => new EmployeeViewModel
                {
                    EmployeeId = u.EmployeeId,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    JoinDate = u.JoiningDate
                })
                .ToList();

            var roster = _context.EmployeeRosterLeave.ToList(); // Fetch all leave records

            //          var leaveCount = _context.TeamLeavesType
            //.GroupBy(t => t.LeaveTypeName)
            //.Select(g => new LeaveCountViewModel
            //{
            //    LeaveType = g.Key,
            //    Count = g.Sum(t => t.DayValue),
            //    color = g.FirstOrDefault().colorPicker // assuming at least one item exists in each group
            //})
            //.ToList();

            var viewModel = new EmployeeViewModel
            {
                Employees = employees,
                Roster = roster

            };

            return View(viewModel);
        }

        [HttpPost]
        [Route("AddRosterLeaves")]
        public IActionResult AddRosterLeaves(string employeeData, DateTime rosterDate, DateTime leaveDate, string note)
        {

            var firstName = _context.Users
    .Where(e => e.EmployeeId == employeeData)
    .Select(e => e.FirstName)
    .FirstOrDefault();
            var lastName = _context.Users
 .Where(e => e.EmployeeId == employeeData)
 .Select(e => e.LastName)
 .FirstOrDefault();

            EmployeeRosterLeave savedLeave = null;
            if (ModelState.IsValid)
            {
                var model = new EmployeeRosterLeave
                {
                    FirstName = firstName,
                    LastName = lastName,
                    EmployeeId = employeeData,
                    RosterDate = rosterDate,
                    LeaveDate = leaveDate,

                    Note = note
                };

                _context.EmployeeRosterLeave.Add(model);
                _context.SaveChanges();

                TempData["rosterId"] = model.Id;
            }
            return RedirectToAction("EmployeeRosterDayOff");
        }
        [HttpDelete]
        [Route("DeleteRosterData/{id}")]
        public async Task<IActionResult> DeleteRosterData(int id)
        {
            var leave = await _context.EmployeeRosterLeave.FindAsync(id);

            if (leave == null)
            {
                return NotFound(new { message = $"Leave with ID {id} not found." });
            }

            _context.EmployeeRosterLeave.Remove(leave);
            await _context.SaveChangesAsync();

            return Ok();
        }
        [HttpPost]
        [Route("UpdateRosterData")]
        public async Task<IActionResult> UpdateRosterData([FromBody] EmployeeLeaveModel model)
        {

            var existingLeave = await _context.EmployeeRosterLeave.FindAsync(model.Id);
            if (existingLeave != null)
            {

                existingLeave.EmployeeId = model.EmployeeId;
                existingLeave.FirstName = model.FullName.Split(' ')[0];
                existingLeave.LastName = model.FullName.Split(' ').Length > 1 ? model.FullName.Split(' ')[1] : "";
                existingLeave.RosterDate = DateTime.Parse(model.StartDate);
                existingLeave.LeaveDate = DateTime.Parse(model.EndDate);

                existingLeave.Note = model.Note;

                _context.Update(existingLeave);
                await _context.SaveChangesAsync();
                return Ok();
            }



            return BadRequest();
        }

        // New Work
        [HttpGet]
        [Route("ApplyLeave")]
        public IActionResult ApplyLeave()
        {
            var employees = new List<EmployeeViewModel>();

            if (User.Identity.IsAuthenticated)
            {
                var emailClaim = User.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Name || c.Type == "Email" || c.Type == "EmailAddress" || c.Type == ClaimTypes.Email);

                string userEmail = emailClaim?.Value;
                Console.WriteLine($"User Email: {userEmail}");
                var currentYear = DateTime.Now.Year;
                if (!string.IsNullOrEmpty(userEmail))
                {
                    var user = _context.Users
     .Where(u => u.Email == userEmail)
     .Select(u => new
     {
         u.Email,
         u.FirstName,
         u.LastName,
         u.Id,
         u.EmployeeId,
         u.Located,
         u.JoiningDate
     })
     .FirstOrDefault();
                    int LeavesDays = 0;
                    int fullLeavePerYear;
                    if (user != null)
                    {
                        if (user.Located == "Office")
                        {
                            fullLeavePerYear = _context.LeavesForOperationAndOffice
                                               .Select(s => s.OfficeValue)
                                               .FirstOrDefault();
                        }
                        else
                        {
                            fullLeavePerYear = _context.LeavesForOperationAndOffice
                                               .Select(s => s.OperationValue)
                                               .FirstOrDefault();
                        }

                        // ➤ ⿢ If joining date exists
                        if (user.JoiningDate != null)
                        {
                            var joiningDate = user.JoiningDate.Value;

                            if (joiningDate.Year < currentYear)
                            {
                                // Joined before this year → full leaves
                                LeavesDays = fullLeavePerYear;
                            }
                            else if (joiningDate.Year == currentYear)
                            {
                                // Joined this year → prorate the leaves

                                int daysInJoiningMonth = DateTime.DaysInMonth(joiningDate.Year, joiningDate.Month);
                                int remainingDaysInJoiningMonth = daysInJoiningMonth - joiningDate.Day;

                                double partialMonth = (double)remainingDaysInJoiningMonth / daysInJoiningMonth;
                                int fullMonthsLeft = 12 - joiningDate.Month;

                                double totalMonths = fullMonthsLeft + partialMonth;

                                double leavePerMonth = (double)fullLeavePerYear / 12; // Auto-calc instead of fixed 1.833

                                LeavesDays = (int)Math.Round(totalMonths * leavePerMonth);
                            }
                        }
                        else
                        {
                            // No joining date → default full leaves
                            LeavesDays = fullLeavePerYear;
                        }


                        int year = DateTime.Now.Year;
                        // Get all weekend mappings for those employees
                        var teamMemberWeekendOffs = _context.TeamMemberWeekendOff
                 .Where(e => e.EmployeeId == user.EmployeeId)
                 .ToList();

                        // Get distinct TeamWeekendOff IDs
                        var teamWeekendOffIds = teamMemberWeekendOffs.Select(e => e.TeamWeekendOffId).Distinct().ToList();

                        // Get TeamWeekendOff info (StartDate, EndDate, DayName)
                        var teamWeekendOffs = _context.TeamWeekendOff// assuming Id is the PK
                            .Select(t => new
                            {
                                t.Id,
                                t.Weekend,         // e.g., "Sunday"
                                t.StartDate,
                                t.EndDate
                            })
                            .ToList();

                        // Create mapping for quick lookup
                        var weekendMap = teamMemberWeekendOffs
                            .Select(member => new
                            {
                                member.EmployeeId,
                                WeekendInfo = teamWeekendOffs.FirstOrDefault(w => w.Id == member.TeamWeekendOffId)
                            })
                            .Where(x => x.WeekendInfo != null)
                            .ToList();

                        // Dictionary<EmployeeId, List<DateTime>> => Sundays or other weekend dates for each employee in the current month
                        var employeeWeekendDates = new Dictionary<string, List<DateTime>>();

                        foreach (var entry in weekendMap)
                        {
                            var empId = entry.EmployeeId;
                            var weekendInfo = entry.WeekendInfo;

                            List<DateTime> weekendDates = new();

                            if (weekendInfo.StartDate == null && weekendInfo.EndDate == null)
                            {
                                // Get day of week (e.g., Sunday, Saturday)
                                if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                                {
                                    for (int month = 1; month <= 12; month++)
                                    {
                                        int daysInMonth = DateTime.DaysInMonth(year, month);

                                        for (int day = 1; day <= daysInMonth; day++)
                                        {
                                            var date = new DateTime(year, month, day);
                                            if (date.DayOfWeek == weekendDay)
                                            {
                                                weekendDates.Add(date);
                                            }
                                        }
                                    }


                                    foreach (var date in weekendDates)
                                    {
                                        Console.WriteLine(date.ToString("yyyy-MM-dd")); // Or use date as needed
                                    }
                                }
                            }


                            else
                            {
                                if (Enum.TryParse<DayOfWeek>(weekendInfo.Weekend, true, out var weekendDay))
                                {
                                    if (weekendInfo.StartDate.HasValue && weekendInfo.EndDate.HasValue)
                                    {
                                        var startDate = weekendInfo.StartDate.Value;
                                        var endDate = weekendInfo.EndDate.Value;



                                        for (var date = startDate; date <= endDate; date = date.AddDays(1))
                                        {
                                            if (date.DayOfWeek == weekendDay)
                                            {
                                                weekendDates.Add(date);
                                            }
                                        }

                                        foreach (var date in weekendDates)
                                        {
                                            Console.WriteLine(date.ToString("yyyy-MM-dd"));
                                        }
                                    }
                                    else
                                    {
                                        // Handle case when StartDate or EndDate is null
                                        Console.WriteLine("StartDate or EndDate is null.");
                                    }
                                }
                            }



                            if (employeeWeekendDates.ContainsKey(empId))
                                employeeWeekendDates[empId].AddRange(weekendDates);
                            else
                                employeeWeekendDates[empId] = weekendDates;
                        }










                        var employeeLocation = _context.Users
                .Where(e => e.EmployeeId == user.EmployeeId)
                .Select(e => e.Located)
                .FirstOrDefault(); // Single string value

                        // Get all holidays
                        var employeeHolidays = _context.EmployeeHolidays
                            .Select(t => new
                            {
                                t.Id,
                                t.Date
                            })
                            .ToList();

                        // Prepare the dictionary
                        var operationEmployeeHolidayDates = new Dictionary<string, List<DateTime>>();

                        // If employee is in Operation team
                        if (employeeHolidays != null && employeeLocation == "Office")
                        {
                            operationEmployeeHolidayDates[user.EmployeeId] = employeeHolidays.Select(h => h.Date).ToList();
                        }

                        var leaveDatesDict = new Dictionary<string, List<(DateTime Date, string Leave, string LeaveType)>>();


                        var pendingLeaves = _context.EmployeeLeaves
             .Where(t =>
                 t.StartDate.HasValue &&
                 t.EndDate.HasValue &&
                 t.EmployeeId == user.EmployeeId &&
                 (
                     t.StartDate >= DateTime.Today ||
                     t.HRApproval == null
                 )
             )
             
    .Select(t => new EmployeeLeaves
    {
        Id = t.Id,
        StartDate = t.StartDate,
        EndDate = t.EndDate,
        Leave = t.Leave,
        HRApproval=t.HRApproval,
        Approval = t.Approval
    })
    .ToList();





                        var countLeaves = pendingLeaves.Count();

                        var employeeLeaves = _context.EmployeeLeaves
       .Where(t => t.StartDate.HasValue && t.EndDate.HasValue && t.EmployeeId == user.EmployeeId && t.LeaveType != null && t.StartDate.Value.Year == currentYear && t.HRApproval=="Approved")
       .Select(t => new
       {
           t.EmployeeId,
           t.StartDate,
           t.EndDate,
           t.Leave,
           t.LeaveType,
           TotalDays = EF.Functions.DateDiffDay(t.StartDate.Value, t.EndDate.Value) + 1
       })
       .ToList();


                        int totalLeaveDays = employeeLeaves.Sum(t => t.TotalDays);

                        // Subtract leave days that fall on holidays
                        var holidayDates = operationEmployeeHolidayDates.ContainsKey(user.EmployeeId)
                            ? operationEmployeeHolidayDates[user.EmployeeId]
                            : new List<DateTime>();

                        int leaveDaysOnHolidays = employeeLeaves
                            .Sum(leave =>
                            {
                                int overlap = 0;
                                for (var d = leave.StartDate.Value.Date; d <= leave.EndDate.Value.Date; d = d.AddDays(1))
                                {
                                    if (holidayDates.Any(h => h.Date == d))
                                        overlap++;
                                }
                                return overlap;
                            });

                        totalLeaveDays = totalLeaveDays - leaveDaysOnHolidays;


                        int EmployeeTotalLeave = Math.Max(0, LeavesDays - totalLeaveDays);
                        if (employeeLeaves.Any())
                        {
                            foreach (var leave in employeeLeaves)
                            {
                                var empId = leave.EmployeeId.ToString(); // ensure string key
                                var startDate = leave.StartDate.Value;
                                var endDate = leave.EndDate.Value;

                                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                                {
                                    // Skip dates that fall on a holiday
                                    if (holidayDates.Any(h => h.Date == date.Date))
                                        continue;

                                    if (!leaveDatesDict.ContainsKey(empId))
                                    {
                                        leaveDatesDict[empId] = new List<(DateTime, string, string)>();
                                    }

                                    leaveDatesDict[empId].Add((date, leave.Leave, leave.LeaveType));
                                }
                            }
                        }



                        var leaveCount = _context.TeamLeavesType
                .GroupBy(t => t.LeaveTypeName)
                .Select(g => new LeaveCountViewModel
                {
                    LeaveType = g.Key,
                    Count = g.Sum(t => t.DayValue),
                    color = g.FirstOrDefault().colorPicker // assuming at least one item exists in each group
                })
                .ToList();

                        var employeefirstName = _context.Users.Where(e => e.EmployeeId == user.EmployeeId).Select(e => e.FirstName).FirstOrDefault();
                        var employeelastName = _context.Users.Where(e => e.EmployeeId == user.EmployeeId).Select(e => e.LastName).FirstOrDefault();
                        var employeeRosterLeave = _context.EmployeeRosterLeave.Where(e => e.EmployeeId == user.EmployeeId).ToList();

                        ViewBag.employeeRosterLeaves = employeeRosterLeave;
                        ViewBag.OperationEmployeeLeaves = leaveDatesDict;
                        ViewBag.OperationleaveCount = leaveCount;
                        ViewBag.OperationEmployeeHolidays = operationEmployeeHolidayDates;
                        ViewBag.EmployeeWeekendDates = employeeWeekendDates;
                        ViewBag.employeeId = user.EmployeeId;
                        ViewBag.FirstName = employeefirstName;
                        ViewBag.LastName = employeelastName;
                        ViewBag.year = year;
                        ViewBag.AwaitingApproval = pendingLeaves;
                        ViewBag.LaevesDays = LeavesDays;
                        ViewBag.TotalEmployeeLeave = EmployeeTotalLeave;
                        ViewBag.PendingLeavesCount = countLeaves;
                    
                    }
                }
            }
            return View(employees);
        }
        [HttpPost]
        [Route("AddEmployeeLeavesByEmployees")]
        public IActionResult AddEmployeeLeavesByEmployees(DateTime startDate, DateTime endDate, string leave, string note, double EmployeePendingLeaves,IFormFile file)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return Unauthorized("User not authenticated.");

            // ---------------------------
            // 2. Get Logged-in UserId (SAFE)
            // ---------------------------
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return BadRequest("UserId not found in claims.");


            // Get employee details
            var employee = _context.Users.FirstOrDefault(p => p.Id == userId);
            if (employee == null)
            {
                Console.WriteLine($"No employee found for email: {userId}");
                return NotFound("Employee not found.");
            }

            string firstName = employee.FirstName ?? "";
            string lastName = employee.LastName ?? "";
            var employeeId = employee.EmployeeId;
            var position = employee.Position;
            var located = employee.Located;

            // Save leave details
            var model = new EmployeeLeaves
            {
                FirstName = firstName,
                LastName = lastName,
                EmployeeId = employeeId,
                StartDate = startDate,
                EndDate = endDate,
                Leave = leave,
                LeaveType = null,
                Note = note,
                Position = position,
                Located = located,
                PendingLeaves = EmployeePendingLeaves

            };

            _context.EmployeeLeaves.Add(model);
            _context.SaveChanges();

            string uniqueFileName = null;
            List<SendSmtpEmailAttachment> emailAttachments = new List<SendSmtpEmailAttachment>();

            if (file != null && file.Length > 0)
            {
                // ⿡ Generate file name
                uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";

                // ⿢ Save file to server
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "leaveData");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    file.CopyTo(stream);

                // ⿣ Save to DB
                var doc = new EmployeeLeaveDocument
                {
                    EmployeeLeaveId = model.Id,
                    EmployeeId = employeeId,
                    FileName = uniqueFileName,
                    FileType = file.ContentType,
                    FileSize = file.Length,
                    FilePath = $"/uploads/leaveData/{uniqueFileName}",
                    UploadedOn = DateTime.Now
                };

                _context.EmployeeLeaveDocument.Add(doc);
                _context.SaveChanges();

                // ⿤ Convert file to BYTE ARRAY (only once)
                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

                // ⿥ Add attachment to Brevo model
                emailAttachments.Add(new SendSmtpEmailAttachment(
                    content: fileBytes,
                    name: file.FileName
           ));
            }
            TempData["LeaveId"] = model.Id;

            try
            {
                string baseUrl = $"{Request.Scheme}://{Request.Host}";
                string downloadUrl = $"{baseUrl}/uploads/leaveData/{uniqueFileName}";
                JObject Params = new JObject
{
    { "StartDate", startDate.ToString("dd-MM-yyyy") },
    { "EndDate", endDate.ToString("dd-MM-yyyy") },
    { "employeeName", $"{firstName} {lastName}" },
    { "employee_email", employee.CommunicationEmail},
    { "employeeId", employeeId },
    { "Note", note },
    { "Leave", leave }
};
                if (file != null)
                {
                    Params.Add("AttachmentName", downloadUrl);
                }
                else
                {
                    Params.Add("AttachmentName", "");
                }

                // ⿡ Get all roles and their emails
                var rolesWithEmails = (from r in _context.Users
                                    
                                       where new[] { "Sales Manager","IT-Digital Team Lead","HR", "Operation Head", "Operation Logistic Executive" }
                                             .Contains(r.Position)
                                       select new
                                       {
                                           RoleName = r.Position,
                                           Email=r.CommunicationEmail
                                       }).ToList();

                // ⿢ Assign each role’s email
                var salesEmail = rolesWithEmails.FirstOrDefault(x => x.RoleName == "Sales Manager")?.Email;
                var digitalTeamLeadEmail = rolesWithEmails.FirstOrDefault(x => x.RoleName == "IT-Digital Team Lead")?.Email;
                var operationHeadEmail = rolesWithEmails.FirstOrDefault(x => x.RoleName == "Operation Head")?.Email;
                var hrRolesEmail = rolesWithEmails.FirstOrDefault(x => x.RoleName == "HR")?.Email;
                var logisticEmail = rolesWithEmails.FirstOrDefault(x => x.RoleName == "Operation Logistic Executive")?.Email;

                string senderName = _configuration["EmailConfiguration:SenderName"];
                string senderEmail = _configuration["EmailConfiguration:SenderEmailClient"];
                string hrEmail = null;
                string mainEmail = null;
                string managerEmail = null;

                // ⿣ Determine HR and main recipient based on employee’s role/location
                if (employee.Located == "Operation")
                {
                    hrEmail = hrRolesEmail;

                    if (employee.Position == "Chef" || employee.Position == "Chef-A")
                    {
                        mainEmail = logisticEmail;
                    }
                    else
                    {
                        mainEmail = operationHeadEmail;
                    }
                }
                //IT
                else if (!string.IsNullOrEmpty(employee.Position) && employee.Position.Contains("IT"))
                {
                    hrEmail = hrRolesEmail;
                    mainEmail = digitalTeamLeadEmail;
                    managerEmail = "subhashpandey24@gmail.com";



                }
                //Sales
                else if (!string.IsNullOrEmpty(employee.Position) && employee.Position.Contains("Sales"))
                {
                    hrEmail = hrRolesEmail;
                    mainEmail = salesEmail;
                }
                //Hr or It
                else if (employee.Position == "HR")
                {

                    hrEmail = "sandeep@trekthehimalayas.com";
                    mainEmail = "Rakesh@trekthehimalayas.com";
                    managerEmail = "subhashpandey24@gmail.com";


                }
                else if (employee.Position == "IT-Digital Team Lead" || employee.Position == "Operation Head" || employee.Position == "Sales Manager")
                {

                    hrEmail = hrRolesEmail;
                    mainEmail = "sandeep@trekthehimalayas.com";
                    managerEmail = "subhashpandey24@gmail.com";

                }


                else
                {
                    hrEmail = hrRolesEmail;
                }

                Console.WriteLine($"ending leave email to HR: {hrEmail}, Main Recipient: {mainEmail}");
                var recipients = new List<string>();
                recipients.Add(hrEmail);

                if (!string.IsNullOrWhiteSpace(mainEmail))
                    recipients.Add(mainEmail);

                if (!string.IsNullOrWhiteSpace(managerEmail))
                    recipients.Add(managerEmail);

                // Send email to all recipients
                foreach (var email in recipients)
                {
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        var toList = new List<SendSmtpEmailTo>
{
    new SendSmtpEmailTo(email)
};

                        _bravoMail.SendWelcomeEmail(
                            senderName,
                            senderEmail,
                            toList,
                            firstName,
                            362,
                            Params,
                            null,
                            "Leave Application",
                            emailAttachments
                        );
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
            }


            return Ok(new { message = "Leave submitted successfully." });
        }




        [HttpPost]
        [Route("UpdateEmployeeLeaveData")]
        public async Task<IActionResult> UpdateEmployeeLeaveData([FromBody] EmployeeLeaveModel model)
        {

            var existingLeave = await _context.EmployeeLeaves.FindAsync(model.Id);
            if (existingLeave != null)
            {


                existingLeave.StartDate = DateTime.Parse(model.StartDate);
                existingLeave.EndDate = DateTime.Parse(model.EndDate);

                existingLeave.Leave = model.Leave;


                _context.Update(existingLeave);
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();



        }
        [HttpPost]
        [Route("OperationAndOfficeLeaves")]
        public IActionResult OperationAndOfficeLeaves(int OperationValue, int OfficeValue)
        {

            if (OperationValue <= 0 || OfficeValue <= 0)
            {
                TempData["Error"] = "Please enter valid numbers for both fields.";
                return RedirectToAction("OperationAndOfficeLeaves");
            }


            var existingRecord = _context.LeavesForOperationAndOffice.FirstOrDefault();

            if (existingRecord != null)
            {
                // Update existing record
                existingRecord.OperationValue = OperationValue;
                existingRecord.OfficeValue = OfficeValue;
                existingRecord.CreatedDate = DateTime.Now;

                _context.LeavesForOperationAndOffice.Update(existingRecord);
            }
            else
            {
                // Insert new record
                var newLeaveSetting = new LeavesForOperationAndOffice
                {
                    OperationValue = OperationValue,
                    OfficeValue = OfficeValue,
                    CreatedDate = DateTime.Now
                };

                _context.LeavesForOperationAndOffice.Add(newLeaveSetting);
            }

            _context.SaveChanges();

            TempData["Success"] = "Total Leaves Days saved successfully!";
            return RedirectToAction("EmployeeLeaveTracker");
        }

        [HttpGet]
        [Route("LeaveForApproval")]
        public IActionResult LeaveForApproval()
        {

            if (!User.Identity.IsAuthenticated)
            {

                return Unauthorized();
            }


            var emailClaim = User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Email ||
                c.Type == "Email" ||
                c.Type == "EmailAddress" ||
                c.Type == ClaimTypes.Name);

            string userEmail = emailClaim?.Value;
            Console.WriteLine($"User Email: {userEmail}");

            if (string.IsNullOrEmpty(userEmail))
            {
                Console.WriteLine("User email is null or empty.");
                return BadRequest("User email not found.");
            }

            var userId = _context.Users
       .Where(t => t.Email == userEmail)
       .Select(t => t.Id)
       .FirstOrDefault();

            var position = _context.Users.Where(t => t.Id == userId).Select(t => t.Position).FirstOrDefault();

            var roleIds = _context.UserRoles
                .Where(t => t.UserId == userId)
                .Select(t => t.RoleId)
                .ToList();

            var roleNames = _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();
           // DigitalTeamLead

            List<EmployeeLeaveVM> employeeLeaves = new List<EmployeeLeaveVM>();
            if (roleNames.Contains("Admin"))
            {
                employeeLeaves = (from t in _context.EmployeeLeaves
                                  join d in _context.EmployeeLeaveDocument
                                       on t.Id equals d.EmployeeLeaveId into docGroup
                                  from doc in docGroup.DefaultIfEmpty()   // LEFT JOIN
                                  where
                                      t.StartDate.HasValue &&
                                      t.EndDate.HasValue &&
                                      t.Approval == null &&
                                      t.Position.Contains("Sales") &&
                                      t.Position != "Sales Manager" &&
                                      t.Located == "Office"
                                  select new EmployeeLeaveVM
                                  {
                                      Leave = t,
                                      Document = doc
                                  })
 .ToList();

            }
            
           
                //Operation
                else if (position.Contains("Operation Head"))
                {
                    employeeLeaves = (from t in _context.EmployeeLeaves
                                      join d in _context.EmployeeLeaveDocument
                                           on t.Id equals d.EmployeeLeaveId into docGroup
                                      from doc in docGroup.DefaultIfEmpty()   // LEFT JOIN
                                      where t.StartDate.HasValue
                        && t.EndDate.HasValue
                     && t.Approval == null
                         && t.Located == "Operation"
                         && t.Position != "Operation Head"
                         && t.Position!= "Operation Logistic Executive"
                         && t.Position != "Chef"
                                && t.Position != "Chef-A"
                                      select new EmployeeLeaveVM
                                      {
                                          Leave = t,
                                          Document = doc
                                      })
    .ToList();


                }

                else if (position.Contains("Operation Logistic Executive"))
                {
                    employeeLeaves = (from t in _context.EmployeeLeaves
                                      join d in _context.EmployeeLeaveDocument
                                           on t.Id equals d.EmployeeLeaveId into docGroup
                                      from doc in docGroup.DefaultIfEmpty()
                                      where t.StartDate.HasValue
                                            && t.EndDate.HasValue
                                            && t.Approval == null
                                            && t.Located == "Operation"
                                            && (t.Position == "Chef"
                                                || t.Position == "Chef-A")
                                      select new EmployeeLeaveVM
                                      {
                                          Leave = t,
                                          Document = doc
                                      }).ToList();
                }
            
            //IT Team
            else if (roleNames.Contains("DigitalTeamLead"))
            {
                employeeLeaves = (from t in _context.EmployeeLeaves
                                  join d in _context.EmployeeLeaveDocument
                                       on t.Id equals d.EmployeeLeaveId into docGroup
                                  from doc in docGroup.DefaultIfEmpty()   // LEFT JOIN
                                  where t.StartDate.HasValue
                     && t.EndDate.HasValue
                     && t.Approval == null
                      && t.Position.Contains("IT") &&
                      t.Position != "IT-Digital Team Lead" &&
                      t.Located == "Office"
                                  select new EmployeeLeaveVM
                                  {
                                      Leave = t,
                                      Document = doc
                                  })
.ToList();

            }
            //HR
            else if (roleNames.Contains("HR"))
            {
                var group1 = (from t in _context.EmployeeLeaves
                              join d in _context.EmployeeLeaveDocument
                                   on t.Id equals d.EmployeeLeaveId into docGroup
                              from doc in docGroup.DefaultIfEmpty()   // LEFT JOIN
                              where
    t.Located != "NULL" &&
        t.Located != "Operation" &&

        !t.Position.Contains("IT") &&
        !t.Position.Contains("Sales")
        && t.Position != "HR" &&
        t.HRApproval == null &&
        t.Approval == null&&
        t.Position.Contains("Operation Logistic Executive")
                              select new EmployeeLeaveVM
                              {
                                  Leave = t,
                                  Document = doc
                              })
                   .ToList();

                var group2 = (from t in _context.EmployeeLeaves
                              join d in _context.EmployeeLeaveDocument
                              on t.Id equals d.EmployeeLeaveId into docGroup
                              from doc in docGroup.DefaultIfEmpty()
                              where
                                  (
                                      (!string.IsNullOrEmpty(t.Position) &&
                                       (t.Position.Contains("IT") || t.Position.Contains("Sales")))
                                      || t.Located == "Operation"
                                  )
                                  && t.Approval != null
                                  && t.HRApproval == null
                              select new EmployeeLeaveVM
                              {
                                  Leave = t,
                                  Document = doc
                              })
                              .ToList();
                var allowedPositions = new[]
{
    "Sales Manager",
    "Operation Head",
    "IT-Digital Team Lead"
};

                var group3 = (from t in _context.EmployeeLeaves
                              join d in _context.EmployeeLeaveDocument
                              on t.Id equals d.EmployeeLeaveId into docGroup
                              from doc in docGroup.DefaultIfEmpty()
                              where
                        !string.IsNullOrEmpty(t.Position) &&
                        allowedPositions.Contains(t.Position) &&
                        t.Approval == null &&
                        t.HRApproval == null
                              select new EmployeeLeaveVM
                              {
                                  Leave = t,
                                  Document = doc
                              })
                              .ToList();

                //            var group3 = _context.EmployeeLeaves
                //.Where(t =>
                //    (
                //        (!string.IsNullOrEmpty(t.Position) &&
                //         (t.Position.Contains("Operation Head") || t.Position.Contains("IT-Digital Team Lead") || t.Position.Contains("Sales Manager")))
                //        || t.Located == "Operation"
                //    )
                //    && t.HRApproval == null
                //)
                //.ToList();



                employeeLeaves = group1.Concat(group2).Concat(group3).ToList();




            }
            else if (roleNames.Contains("Super"))
            {
                employeeLeaves = (from t in _context.EmployeeLeaves
                                  join d in _context.EmployeeLeaveDocument
                                  on t.Id equals d.EmployeeLeaveId into docGroup
                                  from doc in docGroup.DefaultIfEmpty()
                                  where


                t.Position.Contains("HR") &&

        t.HRApproval == null &&
        t.Approval == null
                                  select new EmployeeLeaveVM
                                  {
                                      Leave = t,
                                      Document = doc
                                  })
                .ToList();





            }
            return View(employeeLeaves);
        }


        [HttpGet]
        [Route("HRLeaveForApproval")]
        public IActionResult HRLeaveForApproval()
        {

            if (!User.Identity.IsAuthenticated)
            {

                return Unauthorized();
            }


            var emailClaim = User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Email ||
                c.Type == "Email" ||
                c.Type == "EmailAddress" ||
                c.Type == ClaimTypes.Name);

            string userEmail = emailClaim?.Value;
            Console.WriteLine($"User Email: {userEmail}");

            if (string.IsNullOrEmpty(userEmail))
            {
                Console.WriteLine("User email is null or empty.");
                return BadRequest("User email not found.");
            }

            var userId = _context.Users
       .Where(t => t.Email == userEmail)
       .Select(t => t.Id)
       .FirstOrDefault();

            var roleIds = _context.UserRoles
                .Where(t => t.UserId == userId)
                .Select(t => t.RoleId)
                .ToList();

            var roleNames = _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();



            var employeeLeaves = new List<EmployeeLeaves>();
            List<EmployeeLeaveVM> finalLeaves = new List<EmployeeLeaveVM>();
            //HR
            if (roleNames.Contains("HR"))
            {
                var group1 = (from t in _context.EmployeeLeaves
                              join d in _context.EmployeeLeaveDocument
                              on t.Id equals d.EmployeeLeaveId into docGroup
                              from doc in docGroup.DefaultIfEmpty()

                              where
    !string.IsNullOrEmpty(t.Located) &&
    t.Located != "Operation" &&

    !string.IsNullOrEmpty(t.Position) &&
    !t.Position.ToLower().Contains("IT") &&
    !t.Position.ToLower().Contains("Sales") &&
    t.Position != "HR" &&

    t.HRApproval == null &&
    t.Approval == null
                            
                              select new EmployeeLeaveVM
                              {
                                  Leave = t,
                                  Document = doc
                              })
.ToList();

                var group2 = (from t in _context.EmployeeLeaves
                              join d in _context.EmployeeLeaveDocument
                              on t.Id equals d.EmployeeLeaveId into docGroup
                              from doc in docGroup.DefaultIfEmpty()
                              where
                                  (
                                      (!string.IsNullOrEmpty(t.Position) &&
                                       (t.Position.Contains("IT") || t.Position.Contains("Sales")))
                                      || t.Located == "Operation"
                                  )
                                  && t.Approval != null
                                  && t.HRApproval == null
                              select new EmployeeLeaveVM
                              {
                                  Leave = t,
                                  Document = doc
                              })
                              .ToList();

                var group3 = (from t in _context.EmployeeLeaves
                              join d in _context.EmployeeLeaveDocument
                              on t.Id equals d.EmployeeLeaveId into docGroup
                              from doc in docGroup.DefaultIfEmpty()
                              where
                                  (
                                      (!string.IsNullOrEmpty(t.Position) &&
                                       (t.Position == "Sales Manager" ||
                                        t.Position == "Operation Head" ||
                                        t.Position == "IT-Digital Team Lead" || t.Position == "Operation Logistic Executive"))
                                  )
                                  && t.Approval == null
                                  && t.HRApproval == null
                              select new EmployeeLeaveVM
                              {
                                  Leave = t,
                                  Document = doc
                              })
                              .ToList();



                finalLeaves = group1.Concat(group2).Concat(group3).ToList();




            }
            else if (roleNames.Contains("Super"))
            {
               finalLeaves =
    (from t in _context.EmployeeLeaves
     join d in _context.EmployeeLeaveDocument
         on t.Id equals d.EmployeeLeaveId into docGroup
     from doc in docGroup.DefaultIfEmpty()
     where t.Position.Contains("HR")
           && t.HRApproval == null
           && t.Approval == null
     select new EmployeeLeaveVM
     {
         Leave = t,
         Document = doc
     }).ToList();





            }

            return View(finalLeaves);
        }


        [HttpPost]
        [Route("UpdateLeaveApproval")]
        public IActionResult UpdateLeaveApproval(int id, string status, string leave,string leaveType, string? remarks)
        {
            if (!User.Identity.IsAuthenticated)
            {

                return Unauthorized();
            }


            var emailClaim = User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Email ||
                c.Type == "Email" ||
                c.Type == "EmailAddress" ||
                c.Type == ClaimTypes.Name);

            string userEmail = emailClaim?.Value;

            var userId = _context.Users
      .Where(t => t.Email == userEmail)
      .Select(t => t.Id)
      .FirstOrDefault();

            var firstname = _context.Users.Where(t => t.Email == userEmail).Select(t => t.FirstName).FirstOrDefault();

            var lastname = _context.Users.Where(t => t.Email == userEmail).Select(t => t.LastName).FirstOrDefault();
            var Name = firstname + lastname;

            //       var Email = _context.Users
            //.Where(t => t.Email == userEmail)
            //.Select(t => t.commun)
            //.FirstOrDefault();

            var roleIds = _context.UserRoles
                .Where(t => t.UserId == userId)
                .Select(t => t.RoleId)
                .ToList();

            var roleNames = _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();
            if (userId != null)
            {
                var leaveRecord = _context.EmployeeLeaves.Where(t => t.Id == id).FirstOrDefault();
                var empployeeId = leaveRecord.EmployeeId;
                var employeeCommunicationEmail = _context.Users.Where(t => t.EmployeeId == empployeeId).Select(t => t.CommunicationEmail).FirstOrDefault();
                if (leaveRecord == null)
                {
                    return NotFound(new { success = false, message = "Leave record not found." });
                }


                if (roleNames.Contains("HR") || roleNames.Contains("Super"))
                {
                    if (leave == "WHF")
                    {

                        if (leaveRecord == null)
                        {
                            return NotFound();
                        }

                        if (status == "Approved" && leaveType == "Paid")
                        {
                            leaveRecord.LeaveType = "other";

                        }
                        else if (status == "Approved" && leaveType == "Unpaid")
                        {
                            leaveRecord.LeaveType = "Unpaid";

                        }
                        else
                        {
                            leaveRecord.LeaveType = "Unpaid";
                        }
                        leaveRecord.HRApproval = status;
                        leaveRecord.NoteByHR = remarks;
                        if (leaveRecord.Approval == null)
                        {
                            leaveRecord.Approval = status;
                            leaveRecord.ApprovedBy = userEmail;
                        }

                        _context.EmployeeLeaves.Update(leaveRecord);



                    }
                    else
                    {


                        if (status == "Approved" && leaveType == "Paid")
                        {
                            leaveRecord.LeaveType = "Paid";
                        }
                        else if (status == "Approved" && leaveType == "Unpaid")
                        {
                            leaveRecord.LeaveType = "Unpaid";
                        }
                        else
                        {
                            leaveRecord.LeaveType = "Unpaid";
                        }
                        leaveRecord.HRApproval = status;
                        leaveRecord.NoteByHR = remarks;
                        if (leaveRecord.Approval == null)
                        {
                            leaveRecord.Approval = status;
                            leaveRecord.ApprovedBy = userEmail;
                        }


                        _context.EmployeeLeaves.Update(leaveRecord);
                    }
                    try
                    {



                        JObject Params = new JObject();
                        Params.Add("status", status);
                        Params.Add("StartDate", leaveRecord.StartDate.Value.ToString("dd-MM-yyyy"));
                        Params.Add("EndDate", leaveRecord.EndDate.Value.ToString("dd-MM-yyyy"));
                        Params.Add("employeeName", leaveRecord.FirstName + "" + leaveRecord.LastName);
                        Params.Add("employee_email", userEmail);
                        Params.Add("Remarks", remarks);
                        Params.Add("Leave", leave);

                        if (roleNames.Contains("HR"))
                        {
                            Params.Add("approved", "HR");
                        }
                        else
                        {
                            Params.Add("approved", Name);
                        }
                            








                        string senderName = _configuration["EmailConfiguration:SenderName"];
                        string senderEmail = _configuration["EmailConfiguration:SenderEmailClient"];
                        string employeeEmail = employeeCommunicationEmail;
                      
                        string managerEmail = "subhashpandey24@gmail.com";
                       

                        if (string.IsNullOrEmpty(employeeEmail))
                        {
                            Console.WriteLine("⚠ HR email is null or missing in configuration.");
                        }
                        else
                        {
                            Console.WriteLine($"Sending leave email to HR: {employeeEmail}");
                            _bravoMail.SendEmail(senderName, senderEmail, employeeEmail, leaveRecord.FirstName, 364, Params);
                            _bravoMail.SendEmail(senderName, senderEmail, managerEmail, leaveRecord.FirstName, 364, Params);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error sending HR email: {ex.Message}");
                    }
                }
                else
                {



                    var LeaveFromTable = _context.EmployeeLeaves.FirstOrDefault(t => t.Id == id);
                    if (LeaveFromTable == null)
                    {
                        return NotFound();
                    }


                    LeaveFromTable.Approval = status;
                    LeaveFromTable.ApprovedBy = userEmail;
                    LeaveFromTable.NoteByHR = remarks;

                    _context.EmployeeLeaves.Update(LeaveFromTable);

                    try
                    {



                        JObject Params = new JObject();
                        Params.Add("status", status);
                        Params.Add("StartDate", leaveRecord.StartDate.Value.ToString("dd-MM-yyyy"));
                        Params.Add("EndDate", leaveRecord.EndDate.Value.ToString("dd-MM-yyyy"));
                        Params.Add("employeeName", leaveRecord.FirstName + "" + leaveRecord.LastName);
                        Params.Add("employee_email", userEmail);
                        Params.Add("Remarks", remarks);
                        Params.Add("Leave", leave);
                        Params.Add("approved", Name);








                        string senderName = _configuration["EmailConfiguration:SenderName"];
                        string senderEmail = _configuration["EmailConfiguration:SenderEmailClient"];
                      
                        string managerEmail = "subhashpandey24@gmail.com";
                       



                        if (string.IsNullOrEmpty(managerEmail))
                        {
                            Console.WriteLine("⚠ HR email is null or missing in configuration.");
                        }
                        else
                        {
                            Console.WriteLine($"Sending leave email to HR: {managerEmail}");
                         
                            _bravoMail.SendEmail(senderName, senderEmail, managerEmail, leaveRecord.FirstName, 364, Params);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error sending HR email: {ex.Message}");
                    }
                }
                _context.SaveChanges();
            }
            // update status



            return Json(new { success = true, message = $"Leave {status} successfully!" });
        }


        [HttpGet]
        [Route("AllEmployeeLeaves")]
        public IActionResult AllEmployeeLeaves()
        {
            // Get all leaves where LeaveType is null
            var employeeLeaves = (
    from t in _context.EmployeeLeaves
    join d in _context.EmployeeLeaveDocument
        on t.Id equals d.EmployeeLeaveId into docGroup
    from doc in docGroup.DefaultIfEmpty()
    where t.StartDate.HasValue
         && t.EndDate.HasValue
         && t.LeaveType != null
    orderby t.StartDate descending
    select new EmployeeLeaveVM
    {
        Leave = new EmployeeLeaves
        {
            Id = t.Id,
            EmployeeId = t.EmployeeId,
            FirstName = t.FirstName,
            LastName = t.LastName,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Leave = t.Leave,
            LeaveType = t.LeaveType,
            Note = t.Note,
            HRApproval = t.HRApproval,
            NoteByHR=t.NoteByHR
        },
        Document = doc
    }
).ToList();
            // Pass the model to the view
            return View(employeeLeaves);
        }



        [HttpGet]
        [Route("AllEmployeeLeavesForManger")]
        public IActionResult AllEmployeeLeavesForManger()
        {

            if (!User.Identity.IsAuthenticated)
            {

                return Unauthorized();
            }


            var emailClaim = User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Email ||
                c.Type == "Email" ||
                c.Type == "EmailAddress" ||
                c.Type == ClaimTypes.Name);

            string userEmail = emailClaim?.Value;
            Console.WriteLine($"User Email: {userEmail}");

            if (string.IsNullOrEmpty(userEmail))
            {
                Console.WriteLine("User email is null or empty.");
                return BadRequest("User email not found.");
            }

            var userId = _context.Users
       .Where(t => t.Email == userEmail)
       .Select(t => t.Id)
       .FirstOrDefault();

            var position = _context.Users.Where(t => t.Id == userId).Select(t => t.Position).FirstOrDefault();

            var roleIds = _context.UserRoles
                .Where(t => t.UserId == userId)
                .Select(t => t.RoleId)
                .ToList();

            var roleNames = _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();
            List<EmployeeLeaveVM> employeeLeaves = new List<EmployeeLeaveVM>();

            if (roleNames.Contains("Admin"))
            {

                employeeLeaves = (
    from t in _context.EmployeeLeaves
    join d in _context.EmployeeLeaveDocument
        on t.Id equals d.EmployeeLeaveId into docGroup
    from doc in docGroup.DefaultIfEmpty()
    where t.StartDate.HasValue
         && t.EndDate.HasValue && t.Position.Contains("Sales") &&
                                      t.Position != "Sales Manager" &&
                                      t.Located == "Office"
         && t.LeaveType != null
    orderby t.StartDate descending
    select new EmployeeLeaveVM
    {
        Leave = new EmployeeLeaves
        {
            Id = t.Id,
            EmployeeId = t.EmployeeId,
            FirstName = t.FirstName,
            LastName = t.LastName,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Leave = t.Leave,
            LeaveType = t.LeaveType,
            Note = t.Note,
            HRApproval = t.HRApproval
        },
        Document = doc
    }
).ToList();
               

            }
            
                //Operation
                else if (position.Contains("Operation Head"))
                {

                    employeeLeaves = (
   from t in _context.EmployeeLeaves
   join d in _context.EmployeeLeaveDocument
       on t.Id equals d.EmployeeLeaveId into docGroup
   from doc in docGroup.DefaultIfEmpty()
   where t.StartDate.HasValue
        && t.EndDate.HasValue &&
          t.Located == "Operation"
                         && t.Position != "Operation Head"
                         && t.Position != "Chef"
                                && t.Position != "Chef-A"
        && t.LeaveType != null
   orderby t.StartDate descending
   select new EmployeeLeaveVM
   {
       Leave = new EmployeeLeaves
       {
           Id = t.Id,
           EmployeeId = t.EmployeeId,
           FirstName = t.FirstName,
           LastName = t.LastName,
           StartDate = t.StartDate,
           EndDate = t.EndDate,
           Leave = t.Leave,
           LeaveType = t.LeaveType,
           Note = t.Note,
           HRApproval = t.HRApproval
       },
       Document = doc
   }
).ToList();
                  


                }




                else if (position.Contains("Operation Logistic Executive"))
                {

                    employeeLeaves = (
    from t in _context.EmployeeLeaves
    join d in _context.EmployeeLeaveDocument
        on t.Id equals d.EmployeeLeaveId into docGroup
    from doc in docGroup.DefaultIfEmpty()
    where t.StartDate.HasValue
         && t.EndDate.HasValue
           && t.Located == "Operation"
                                            && (t.Position == "Chef"
                                                || t.Position == "Chef-A")
         && t.LeaveType != null
    orderby t.StartDate descending
    select new EmployeeLeaveVM
    {
        Leave = new EmployeeLeaves
        {
            Id = t.Id,
            EmployeeId = t.EmployeeId,
            FirstName = t.FirstName,
            LastName = t.LastName,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Leave = t.Leave,
            LeaveType = t.LeaveType,
            Note = t.Note,
            HRApproval = t.HRApproval
        },
        Document = doc
    }
).ToList();

                }
            
            //IT Team
            else if (roleNames.Contains("DigitalTeamLead"))
            {
                employeeLeaves = (
from t in _context.EmployeeLeaves
join d in _context.EmployeeLeaveDocument
   on t.Id equals d.EmployeeLeaveId into docGroup
from doc in docGroup.DefaultIfEmpty()
where t.StartDate.HasValue
    && t.EndDate.HasValue
     && t.Position.Contains("IT") &&
                  t.Position != "IT-Digital Team Lead" &&
                  t.Located == "Office"
    && t.LeaveType != null
orderby t.StartDate descending
select new EmployeeLeaveVM
{
   Leave = new EmployeeLeaves
   {
       Id = t.Id,
       EmployeeId = t.EmployeeId,
       FirstName = t.FirstName,
       LastName = t.LastName,
       StartDate = t.StartDate,
       EndDate = t.EndDate,
       Leave = t.Leave,
       LeaveType = t.LeaveType,
       Note = t.Note,
       HRApproval = t.HRApproval
   },
   Document = doc
}
).ToList();



            }
            //HR
           
            else if (roleNames.Contains("Super"))
            {
                employeeLeaves = (
    from t in _context.EmployeeLeaves
    join d in _context.EmployeeLeaveDocument
        on t.Id equals d.EmployeeLeaveId into docGroup
    from doc in docGroup.DefaultIfEmpty()
    where t.StartDate.HasValue
         && t.EndDate.HasValue
         && t.LeaveType != null
    orderby t.StartDate descending
    select new EmployeeLeaveVM
    {
        Leave = new EmployeeLeaves
        {
            Id = t.Id,
            EmployeeId = t.EmployeeId,
            FirstName = t.FirstName,
            LastName = t.LastName,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Leave = t.Leave,
            LeaveType = t.LeaveType,
            Note = t.Note,
            HRApproval = t.HRApproval
        },
        Document = doc
    }
).ToList();





            }
           
            return View(employeeLeaves);
        }

        [HttpPost]
        [Route("DeleteLeaveByEmployee/{id}")]
        public async Task<IActionResult> DeleteLeaveByEmployee(int id)
        {
            // ✅ Use FirstOrDefaultAsync for async database operation
            var leave = await _context.EmployeeLeaves
                .Where(t => t.Id == id && t.Approval == null && t.HRApproval==null)
                .FirstOrDefaultAsync();

            if (leave == null)
            {
                return NotFound(new { message = $"Leave already approved/rejected you can't delete." });
            }

            _context.EmployeeLeaves.Remove(leave);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Leave deleted successfully." });
        }

        [HttpPost]
        [Route("NewUpdateEmployeeLeave")]
        public async Task<IActionResult> NewUpdateEmployeeLeave([FromBody] EmployeeLeaveModel model)
        {

            var existingLeave = await _context.EmployeeLeaves.FindAsync(model.Id);
            if (existingLeave != null)
            {


                existingLeave.StartDate = DateTime.Parse(model.StartDate);
                existingLeave.EndDate = DateTime.Parse(model.EndDate);
                existingLeave.LeaveType = model.LeaveType;
                existingLeave.HRApproval = model.HRApproval;



                _context.Update(existingLeave);
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();



        }
    }
}