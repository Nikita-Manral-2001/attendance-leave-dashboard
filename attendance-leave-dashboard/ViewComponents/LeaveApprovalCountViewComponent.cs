using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TTH.Areas.Super.Data;

namespace TTH.Areas.Super.ViewComponents
{
    public class LeaveApprovalCountViewComponent : ViewComponent
    {
        private readonly AppDataContext _context;
        private readonly IConfiguration _configuration;

        public LeaveApprovalCountViewComponent(AppDataContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IViewComponentResult Invoke()
        {
            var claimsPrincipal = User as ClaimsPrincipal;
            if (claimsPrincipal == null)
                return Content("");

            if (!claimsPrincipal.Identity.IsAuthenticated)
                return Content("");

            var email = claimsPrincipal.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Name ||
                c.Type == "Email" ||
                c.Type == "EmailAddress")?.Value;

            if (string.IsNullOrEmpty(email))
                return Content("");

            var userId = _context.Users
                .Where(u => u.Email == email)
                .Select(u => u.Id)
                .FirstOrDefault();

            var roleIds = _context.UserRoles
                .Where(r => r.UserId == userId)
                .Select(r => r.RoleId)
                .ToList();

            var roleNames = _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();
            var position = _context.Users.Where(t => t.Id == userId).Select(t => t.Position).FirstOrDefault();


            int count = 0;

            // Add your logic here
            if (roleNames.Contains("Admin"))
            {
                count = _context.EmployeeLeaves.Count(t =>
                       t.StartDate.HasValue &&
                       t.EndDate.HasValue &&
                       t.Approval == null &&
                       t.Position.Contains("Sales") &&
                       t.Located == "Office");
            }

           
                else if (position.Contains("Operation Head"))
                {
                    count = _context.EmployeeLeaves.Count(t =>
                           t.StartDate.HasValue &&
                           t.EndDate.HasValue &&
                           t.Approval == null &&
                             t.Position != "Chef"
                                && t.Position != "Chef-A"&&
                           t.Located == "Operation" &&
                           t.Position != "Operation Head");
                }
                else if (position.Contains("Operation Logistic Executive"))
                {
                    count = _context.EmployeeLeaves.Count(t =>
                           t.StartDate.HasValue &&
                           t.EndDate.HasValue &&
                           t.Approval == null &&
                               (t.Position == "Chef"
                                                || t.Position == "Chef-A")&&
                           t.Located == "Operation" &&
                           t.Position != "Operation Head");
                }
            
            else if (roleNames.Contains("DigitalTeamLead"))
            {
                count = _context.EmployeeLeaves.Count(t =>
                       t.StartDate.HasValue &&
                       t.EndDate.HasValue &&
                       t.Approval == null &&
                       t.Position.Contains("IT") &&
                       t.Located == "Office");
            }
            else if (roleNames.Contains("Super"))
            {
                count = _context.EmployeeLeaves.Count(t =>
                       t.Position.Contains("HR") &&
                       t.HRApproval == null &&
                       t.Approval == null);
            }
            else if (roleNames.Contains("HR"))
            {
                var group1 = _context.EmployeeLeaves
                    .Where(t =>
                        t.Located != "NULL" &&
                        t.Located != "Operation" &&
                        !t.Position.Contains("IT") &&
                        !t.Position.Contains("Sales") &&
                        t.Position != "HR" &&
                        t.HRApproval == null &&
                        t.Approval == null)
                    .Count();

                var group2 = _context.EmployeeLeaves
                    .Where(t =>
                        (
                            (!string.IsNullOrEmpty(t.Position) &&
                             (t.Position.Contains("IT") || t.Position.Contains("Sales"))) ||
                            t.Located == "Operation"
                        ) &&
                        t.Approval != null &&
                        t.HRApproval == null)
                    .Count();

                var group3 = _context.EmployeeLeaves
                    .Where(t =>
                        (
                            (!string.IsNullOrEmpty(t.Position) &&
                             (t.Position == "Sales Manager" ||
                              t.Position == "Operation Head" ||
                              t.Position == "IT-Digital Team Lead" || t.Position == "Operation Logistic Executive"))
                        ) &&
                        t.Approval == null &&
                        t.HRApproval == null)
                    .Count();

                count = group1 + group2 + group3;
            }
            return View(count);
        }
    }
}
