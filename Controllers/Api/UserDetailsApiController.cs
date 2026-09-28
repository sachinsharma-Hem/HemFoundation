using ClosedXML.Excel;
using HemFoundation.Data;
using HemFoundation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text;

namespace HemFoundation.Controllers.Api
{
    [Route("api/UserDetails")]
    [ApiController]
    public class UserDetailsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserDetailsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("Roles")]
        public async Task<IActionResult> GetRoles()
        {
            var result = await (
                from aur in _context.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>()
                join ar in _context.AspNetRoles
                    on aur.RoleId equals ar.Id
                group aur by new
                {
                    ar.Id,
                    ar.Name
                }
                into g
                select new
                {
                    roleId = g.Key.Id,
                    roleName = g.Key.Name,
                    totalUsers = g.Count()
                }
            )
            .OrderByDescending(x => x.totalUsers)
            .ToListAsync();

            return Ok(result);
        }

        [HttpGet("List")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? role = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1)
                page = 1;

            if (pageSize < 10)
                pageSize = 10;

            if (pageSize > 200)
                pageSize = 200;

            var query = _context.VwUserRoleDetails
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(x => x.RoleName == role);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(x =>
                    (x.Username != null && x.Username.Contains(search)) ||
                    (x.FullName != null && x.FullName.Contains(search)) ||
                    (x.Emailid != null && x.Emailid.Contains(search)) ||
                    (x.Mobileno != null && x.Mobileno.Contains(search)) ||
                    (x.AspnetUserId != null && x.AspnetUserId.Contains(search)) ||
                    (x.Usercode != null && x.Usercode.Contains(search))
                );
            }

            var totalRecords = await query.CountAsync();

            var data = await query
                .OrderBy(x => x.Userid)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(
                    totalRecords / (double)pageSize
                ),
                data
            });
        }

        [HttpGet("Export")]
        public async Task<IActionResult> Export(
            [FromQuery] string? role = null,
            [FromQuery] string? search = null,
            [FromQuery] string format = "csv")
        {
            var query = _context.VwUserRoleDetails
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(x => x.RoleName == role);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(x =>
                    (x.Username != null && x.Username.Contains(search)) ||
                    (x.FullName != null && x.FullName.Contains(search)) ||
                    (x.Emailid != null && x.Emailid.Contains(search)) ||
                    (x.Mobileno != null && x.Mobileno.Contains(search)) ||
                    (x.AspnetUserId != null && x.AspnetUserId.Contains(search)) ||
                    (x.Usercode != null && x.Usercode.Contains(search))
                );
            }

            var data = await query
                .OrderBy(x => x.Userid)
                .ToListAsync();

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                return GenerateCsv(data);
            }

            if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
            {
                return GenerateExcel(data);
            }

            return BadRequest("Supported formats: csv, excel");
        }

        private IActionResult GenerateCsv(
            List<VwUserRoleDetails> data)
        {
            var properties = typeof(VwUserRoleDetails)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var sb = new StringBuilder();

            sb.AppendLine(
                string.Join(
                    ",",
                    properties.Select(x => Csv(x.Name))
                )
            );

            foreach (var item in data)
            {
                sb.AppendLine(
                    string.Join(
                        ",",
                        properties.Select(x =>
                            Csv(x.GetValue(item))
                        )
                    )
                );
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            return File(
                bytes,
                "text/csv",
                $"UserDetails_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            );
        }

        private IActionResult GenerateExcel(
            List<VwUserRoleDetails> data)
        {
            var properties = typeof(VwUserRoleDetails)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("User Details");

            for (int column = 0; column < properties.Length; column++)
            {
                var cell = worksheet.Cell(1, column + 1);

                cell.Value = properties[column].Name;

                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;
            }

            for (int row = 0; row < data.Count; row++)
            {
                for (int column = 0; column < properties.Length; column++)
                {
                    var value = properties[column]
                        .GetValue(data[row]);

                    var cell = worksheet.Cell(
                        row + 2,
                        column + 1
                    );

                    if (value != null)
                    {
                        cell.Value = value.ToString();
                    }
                }
            }

            worksheet.SheetView.FreezeRows(1);

            worksheet.Range(
                1,
                1,
                Math.Max(1, data.Count + 1),
                properties.Length
            ).SetAutoFilter();

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"UserDetails_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        private static string Csv(object? value)
        {
            if (value == null)
                return string.Empty;

            var text = value.ToString() ?? string.Empty;

            if (text.Contains(',') ||
                text.Contains('"') ||
                text.Contains('\r') ||
                text.Contains('\n'))
            {
                text = "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            return text;
        }
    }
}