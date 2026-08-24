using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace HemFoundation.Data.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // ==========================================================
        // CREATE ALL ROLES
        // ==========================================================
        string[] roles =
        {
            "Admin",
            "Teacher",
            "Student",
            "School",
            "Parent",
            "Affiliate",
            "Volunteer",
            "TesterEmployee"
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // ==========================================================
        // CREATE DEFAULT ADMIN USER
        // ==========================================================
        var user = await userManager.FindByNameAsync("admin");

        if (user == null)
        {
            user = new ApplicationUser
            {
                FullName = "System Administrator",
                UserName = "admin",
                UserCode = "ADM001",
                Email = "admin@hemfoundation.org",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, "Admin@123");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Admin");
            }
        }
    }
}