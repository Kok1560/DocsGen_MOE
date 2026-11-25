using DocGen.Components;
using DocGen.Data;
using DocGen.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);   // For LocalHost

Directory.CreateDirectory("Storage");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<Loc>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<DocxEngine>();
builder.Services.AddScoped<TemplateService>();
builder.Services.Configure<PdfConverterOptions>(builder.Configuration.GetSection("LibreOffice"));
builder.Services.AddSingleton<PdfConverter>();
builder.Services.AddSingleton<IFileStore, LocalFileStore>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ShareService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "docgen.auth";
        options.Cookie.HttpOnly = true;
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Create or update the database automatically when the app starts
await using (var db = await app.Services
    .GetRequiredService<IDbContextFactory<AppDbContext>>()
    .CreateDbContextAsync())
{
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Language: Arabic by default; the choice is kept in a cookie (see /set-language below)
var supportedCultures = new[] { "ar", "en" };
var localization = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localization.RequestCultureProviders = [new CookieRequestCultureProvider()];   // only the cookie decides, not the browser language
app.UseRequestLocalization(localization);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Language switch button: saves the choice in a cookie and returns to the same page
app.MapGet("/set-language", (string? culture, string? returnUrl, HttpContext http) =>
{
    var lang = culture == "en" ? "en" : "ar";
    http.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, Path = "/" });

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
        ? returnUrl
        : "/";
    return Results.LocalRedirect(target);
}).AllowAnonymous();

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapGet("/s/{token}/pdf", async (string token, bool? download, HttpContext http, ShareService shares) =>
{
    var file = await shares.GetPdfAsync(token);
    if (file is null) return Results.NotFound();   // wrong, revoked and expired links all look the same

    http.Response.Headers.CacheControl = "no-store";
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";

    return download == true
        ? Results.File(file.Content, file.ContentType, file.FileName)   // saves as a file
        : Results.File(file.Content, file.ContentType);                 // shows inside the page
}).AllowAnonymous();

app.MapGet("/download/{id:guid}/{format}", async (Guid id, string format, ClaimsPrincipal user, DocumentService documents) =>
{
    var isPdf = format.Equals("pdf", StringComparison.OrdinalIgnoreCase);
    if (!isPdf && !format.Equals("docx", StringComparison.OrdinalIgnoreCase))
        return Results.NotFound();

    // Returns null for documents that don't exist AND for other people's documents
    var file = await documents.GetFileAsync(user.GetUserId(), id, isPdf);
    return file is null ? Results.NotFound() : Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization();

app.MapGet("/view/{id:guid}", async (Guid id, HttpContext http, ClaimsPrincipal user, DocumentService documents) =>
{
    // Returns null for documents that don't exist AND for other people's documents
    var file = await documents.GetFileAsync(user.GetUserId(), id, pdf: true);
    if (file is null) return Results.NotFound();

    http.Response.Headers.CacheControl = "no-store";
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // No file name given, so the browser shows the PDF instead of downloading it
    return Results.File(file.Content, file.ContentType);
}).RequireAuthorization();

app.Run();
