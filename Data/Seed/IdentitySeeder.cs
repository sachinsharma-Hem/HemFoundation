using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HemFoundation.Data.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        var context =
            serviceProvider.GetRequiredService<ApplicationDbContext>();

        // =========================================================
        // 1. Roles Seed
        // =========================================================
        // IdentityRole / RoleManager use nahi kar rahe hain.
        // Isse ConcurrencyStamp ki koi requirement nahi rahegi.
        // =========================================================

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


        //foreach (var roleName in roles)
        //{
        //    // Directly aspnetroles se check
        //    var roleExists = await context.AspNetRoles
        //        .Where(r => r.Name == roleName)
        //        .Select(r => r.Id)
        //        .AnyAsync();

        //    if (!roleExists)
        //    {
        //        var roleId = Guid.NewGuid().ToString();

        //        // Direct SQL INSERT
        //        // ConcurrencyStamp intentionally nahi hai.
        //        await context.Database.ExecuteSqlInterpolatedAsync($@"
        //            INSERT INTO public.aspnetroles
        //                (id, ""Name"")
        //            VALUES
        //                ({roleId}, {roleName})
        //        ");
        //    }
        //}

        //foreach (var roleName in roles)
        //{
        //    var roleExists = await context.Roles.AnyAsync(r => r.Name == roleName);
        //    if (!roleExists)
        //    {
        //        context.Roles.Add(new IdentityRole
        //        {
        //            Id = Guid.NewGuid().ToString(),
        //            Name = roleName
        //        });
        //    }
        //}
        //await context.SaveChangesAsync();


        // =========================================================
        // 2. Default Admin User
        // =========================================================
        // Abhi aapne Admin user creation comment ki hui hai,
        // isliye yahan kuch nahi karna.
        //
        // Agar future me Admin user create karna ho to bhi
        // UserManager use nahi karenge, direct insert karenge.
        // =========================================================
    }
}