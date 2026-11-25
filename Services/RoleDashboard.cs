using HemFoundation.Data;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Services
{
    /// <summary>
    /// Dashboard for admin roles other than Admin / WJ_Admin / Supervisor: it shows the menus
    /// given to the role (Role rights page) and a summary card for each module that has one.
    /// To change what a role sees, change the role's menus; nothing else needs setting.
    /// </summary>
    public static class RoleDashboard
    {
        public class MenuItem
        {
            public long Id { get; set; }
            public long? ParentId { get; set; }
            public string Name { get; set; } = "";
            public string Url { get; set; } = "";
            public string? Icon { get; set; }
        }

        public class ViewModel
        {
            public string Role { get; set; } = "";
            public string Name { get; set; } = "";
            public List<MenuItem> Menus { get; set; } = new();
            public List<string> Widgets { get; set; } = new();
        }

        /// <summary>Summary cards: widget key → the menu (controller part of its link) that unlocks it.</summary>
        public static readonly (string Widget, string[] Controllers)[] WidgetMenus =
        {
            ("congratulations", new[] { "congratulations" }),
            ("evaluation", new[] { "projectevaluation" }),
            ("sampark", new[] { "hemsampark" }),
            ("users", new[] { "usermanagement" })
        };

        public static async Task<List<MenuItem>> MenusForRoleAsync(ApplicationDbContext context, string roleName)
        {
            var roleIds = await context.Roles.Where(r => r.Name == roleName).Select(r => r.Id).ToListAsync();
            if (roleIds.Count == 0) return new();

            var menuIds = await context.RoleMenuRights
                .Where(rm => roleIds.Contains(rm.RoleId) && rm.CanView == true && rm.IsActive == true && rm.IsDeleted == false)
                .Select(rm => rm.MenuId).Distinct().ToListAsync();

            return await context.Menus.AsNoTracking()
                .Where(m => menuIds.Contains(m.Id) && m.IsActive == true && m.IsDeleted == false)
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new MenuItem
                {
                    Id = m.Id,
                    ParentId = m.ParentId,
                    Name = m.MenuName,
                    Url = m.RouteUrl ?? (m.Controller != null ? "/" + m.Controller + "/" + (m.Action ?? "Index") : ""),
                    Icon = m.Icon
                })
                .ToListAsync();
        }

        /// <summary>First part of a menu link, lower case: "/Congratulations/Index" → "congratulations".</summary>
        public static string ControllerOf(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            var path = url.Split('?', '#')[0].Trim('/');
            var first = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return first.ToLowerInvariant();
        }

        public static List<string> WidgetsFor(IEnumerable<MenuItem> menus)
        {
            var controllers = menus.Select(m => ControllerOf(m.Url)).ToHashSet();
            return WidgetMenus.Where(w => w.Controllers.Any(controllers.Contains)).Select(w => w.Widget).ToList();
        }
    }
}
