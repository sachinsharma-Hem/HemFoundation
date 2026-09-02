using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HemFoundation.Data.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = new PasswordHasher<ApplicationUser>();

        // 1. Seed Roles
        string[] roles =
        {
            "WJ_Admin",
            "WJ_Teacher",
            "WJ_Students",
            "WJ_School",
            "HF_PARENT",
            "WJ_Affiliate",
            "HF_Volunteers"
        };

        foreach (var roleName in roles)
        {
            var roleExists = await context.Roles.AnyAsync(r => r.Name == roleName);
            if (!roleExists)
            {
                context.Roles.Add(new IdentityRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = roleName
                });
            }
        }
        await context.SaveChangesAsync();

        // 2. Seed Default Admin User Safely via EF Core
        //var adminUser = await context.Users.FirstOrDefaultAsync(u => u.UserName == "admin");

        //if (adminUser == null)
        //{
        //    var userId = Guid.NewGuid().ToString();
        //    var hashedPassword = passwordHasher.HashPassword(null!, "Admin@123");

        //    // Use standard EF Core tracking insertion to avoid conflict constraint errors
        //    var newUser = new ApplicationUser
        //    {
        //        Id = userId,
        //        UserName = "admin",
        //        Email = "admin@hemfoundation.org",
        //        EmailConfirmed = true,
        //        PasswordHash = hashedPassword,
        //        SecurityStamp = Guid.NewGuid().ToString(),
        //        LockoutEnabled = false,
        //        AccessFailedCount = 0
        //    };

        //    context.Users.Add(newUser);
        //    await context.SaveChangesAsync();

        //    // 3. Assign WJ_Admin role
        //    var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "WJ_Admin");
        //    if (adminRole != null)
        //    {
        //        var hasRole = await context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRole.Id);
        //        if (!hasRole)
        //        {
        //            context.UserRoles.Add(new IdentityUserRole<string>
        //            {
        //                UserId = userId,
        //                RoleId = adminRole.Id
        //            });
        //            await context.SaveChangesAsync();
        //        }
        //    }
        //}
    }
}