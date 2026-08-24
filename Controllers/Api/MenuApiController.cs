using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Helpers;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/menu")]
    [ApiController]
    public class MenuApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MenuApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. GET ALL MENUS (For Listing Table & Parent Dropdown)
        [HttpGet("list")]
        public async Task<IActionResult> GetMenuList()
        {
            var menus = await _context.Menus
                .Where(m => m.IsDeleted == false)
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new {
                    m.Id,
                    m.ParentId,
                    m.MenuName,
                    m.Area,
                    m.Controller,
                    m.Action,
                    m.RouteUrl,
                    m.Icon,
                    m.DisplayOrder,
                    m.MenuType,
                    m.IsVisible,
                    m.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = menus });
        }

        // 2. GET SINGLE MENU BY ID (For Edit Modal Pre-fill)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMenuById(long id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null || menu.IsDeleted)
                return NotFound(new { success = false, message = "Menu not found." });

            return Ok(new { success = true, data = menu });
        }

        // 3. SAVE MENU (Handles Both Add & Update)
        [HttpPost("save")]
        public async Task<IActionResult> SaveMenu([FromBody] MenuDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Please fill all required fields correctly." });

            try
            {
                if (dto.Id == 0)
                {
                    // ADD NEW MENU
                    var newMenu = new Menu
                    {
                        ParentId = dto.ParentId == 0 ? null : dto.ParentId,
                        MenuName = dto.MenuName,
                        Area = dto.Area,
                        Controller = dto.Controller,
                        Action = dto.Action,
                        RouteUrl = dto.RouteUrl,
                        Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "bi bi-folder" : dto.Icon,
                        DisplayOrder = dto.DisplayOrder,
                        MenuType = dto.ParentId > 0 ? 1 : 0,
                        PortalType = dto.PortalType,
                        IsVisible = dto.IsVisible,
                        IsActive = dto.IsActive,
                        IsDeleted = false,
                        CreatedOn = DateTimeHelper.UtcNow
                    };

                    _context.Menus.Add(newMenu);
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, message = "Menu created successfully!" });
                }
                else
                {
                    // UPDATE EXISTING MENU
                    var existingMenu = await _context.Menus.FindAsync(dto.Id);
                    if (existingMenu == null || existingMenu.IsDeleted)
                        return NotFound(new { success = false, message = "Menu not found." });

                    existingMenu.ParentId = dto.ParentId == 0 ? null : dto.ParentId;
                    existingMenu.MenuName = dto.MenuName;
                    existingMenu.Area = dto.Area;
                    existingMenu.Controller = dto.Controller;
                    existingMenu.Action = dto.Action;
                    existingMenu.RouteUrl = dto.RouteUrl;
                    existingMenu.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "bi bi-folder" : dto.Icon;
                    existingMenu.DisplayOrder = dto.DisplayOrder;
                    existingMenu.MenuType = dto.ParentId > 0 ? 1 : 0;
                    existingMenu.IsVisible = dto.IsVisible;
                    existingMenu.IsActive = dto.IsActive;
                    existingMenu.UpdatedOn = DateTimeHelper.UtcNow;

                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, message = "Menu updated successfully!" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while saving: " + ex.Message });
            }
        }

        // 4. SOFT DELETE MENU
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteMenu(long id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null || menu.IsDeleted)
                return NotFound(new { success = false, message = "Menu not found." });

            // Soft delete
            menu.IsDeleted = true;
            menu.IsActive = false;
            menu.UpdatedOn = DateTimeHelper.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Menu deleted successfully!" });
        }
    }
}