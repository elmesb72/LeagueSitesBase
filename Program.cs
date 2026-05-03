global using Newtonsoft.Json;
global using static LeagueSitesBackend.Models.Extensions;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("Sqlite");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<LeagueSitesContext>(options =>
    {
        options.UseSqlite(connectionString);
    });
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
    });

builder.Services.AddScoped<IPermissionsService, PermissionsService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<IScheduleImportService, ScheduleImportService>();
builder.Services.AddLeagueSitesAuthorization();

builder.Services.AddControllers().AddJsonOptions(o =>
                o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddHttpClient();

builder.Services.Configure<ForwardedHeadersOptions>(options => 
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    var kestrelSection = context.Configuration.GetSection("Kestrel");

    serverOptions.Configure(kestrelSection);
});

var app = builder.Build();

// Exception logging — must be early in pipeline to catch all downstream errors
app.UseMiddleware<ExceptionLoggingMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve tenant-specific static assets from the persistent volume at
// /var/db/static/. In production this is handled by Apache aliases for
// /images/ and /files/, but in dev (no Apache) this lets the backend
// serve them directly so URLs like /images/social/Facebook.webp work.
// Production is unaffected: Apache aliases fire before the request ever
// reaches Kestrel.
var staticVolume = "/var/db/static";
if (Directory.Exists(staticVolume))
{
    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.Combine(staticVolume, "images")),
        RequestPath = "/images"
    });
    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.Combine(staticVolume, "files")),
        RequestPath = "/files"
    });
}

app.UseCookiePolicy();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
