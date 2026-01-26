using StashPup.AspNetCore.Extensions;
using StashPup.AspNetCore.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddStashPup(stash => stash
    .UseLocalStorage(options =>
    {
        options.BasePath = "./uploads";
        options.BaseUrl = "/filestore";  // Changed from /files to avoid conflict
        options.MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
        options.AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".pdf", ".txt", ".doc", ".docx"];
        options.AllowedContentTypes = ["image/*", "application/pdf", "text/*", "application/msword"];
        options.AutoCreateDirectories = true;
    }));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.UseStashPup();
app.MapStashPupEndpoints("/api/files");

app.MapRazorPages();

app.Run();
