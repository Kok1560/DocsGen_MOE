using DocGen.Components;
using DocGen.Data;
using DocGen.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory("Storage");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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

app.Run();
