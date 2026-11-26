using HemFoundation.Common;
using HemFoundation.Data;
using HemFoundation.Data.Seed;
using HemFoundation.Helpers;
using HemFoundation.Models;
using HemFoundation.Models.Identity;
using HemFoundation.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rotativa.AspNetCore;
using System.Text;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient("InHouseFileStorage");


builder.Services.AddScoped<IPasswordMismatchRepository,
    PasswordMismatchRepository>();
builder.Services.AddScoped<ICertificateManager, CertificateManager>();

builder.Services.AddScoped<IPasswordHasher<IdentityUser>,
    PasswordHasher<IdentityUser>>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
    options.User.RequireUniqueEmail = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
builder.Services.AddScoped<ExamBAL>();
var jwtSettings = builder.Configuration.GetSection("JwtSettings");



var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(secretKey),

        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("JWT FAILED");
            Console.WriteLine(context.Exception);
            return Task.CompletedTask;
        },

        OnTokenValidated = context =>
        {
            Console.WriteLine("JWT VALID");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<AttendanceBackgroundWorker>();
builder.Services.AddTransient<SMSManager>();
builder.Services.AddScoped<IGroupProjectService, GroupProjectService>();
builder.Services.AddScoped<IFileStorageService,
    InHouseFileStorageService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ExceptionLoggerService>();

// HEM Sampark: business clock, PDF generation, access checks
builder.Services.AddSingleton<HemFoundation.Services.HemSampark.HemSamparkClock>();
builder.Services.AddScoped<HemFoundation.Services.HemSampark.HemSamparkPdfService>();
builder.Services.AddScoped<HemFoundation.Services.HemSampark.HemSamparkAccess>();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<HemFoundation.Services.Academic.AcademicService>();
builder.Services.AddScoped<HemFoundation.Services.Parent.ParentService>();
builder.Services.AddScoped<HemFoundation.Services.HemStore.HemStoreBridge>();
// Affiliate incentive: month-wise summaries (batched database access)
builder.Services.AddScoped<HemFoundation.Services.AffiliateIncentive.AffiliateIncentiveService>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit =
            110L * 1024 * 1024;
    });
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// 6. Routing (MVC & API support)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
EmailManager.Initialize(app.Configuration);
// 7. Seed Admin Data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await IdentitySeeder.SeedAdminAsync(services);
}


var chromiumPath = Path.Combine(
    app.Environment.ContentRootPath,
    "Chromium"
);

if (!Directory.Exists(chromiumPath))
{
    Directory.CreateDirectory(chromiumPath);
}

var browserFetcher = new PuppeteerSharp.BrowserFetcher(
    new PuppeteerSharp.BrowserFetcherOptions
    {
        Path = chromiumPath
    });

var installedBrowser = await browserFetcher.DownloadAsync();

Console.WriteLine(
    $"Chromium downloaded: {installedBrowser.GetExecutablePath()}"
);
RotativaConfiguration.Setup(
    app.Environment.WebRootPath,
    "Rotativa"
);

app.Run();