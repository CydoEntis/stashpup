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

// Enable all endpoints including new v0.2.0 folder operations
app.MapStashPupEndpoints("/api/files", options =>
{
    options.EnableUpload = true;
    options.EnableDownload = true;
    options.EnableDelete = true;
    options.EnableMetadata = true;
    options.EnableList = false; // Keep disabled for security
    options.EnableFolderList = true; // NEW: Enable folder listing
    options.EnableFolderDelete = true; // NEW: Enable folder deletion
    options.EnableBulkMove = true; // NEW: Enable bulk move
});

app.MapRazorPages();

app.Run();
