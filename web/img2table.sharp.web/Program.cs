
using img2table.sharp.web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

var rootFolder = Path.Combine(Path.GetTempPath(), WorkDirectoryOptions.RootFolderName);
if (!Directory.Exists(rootFolder))
{
    Directory.CreateDirectory(rootFolder);
}

builder.Services.Configure<WorkDirectoryOptions>(opt =>
{
    opt.RootFolder = rootFolder;
});


var app = builder.Build();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(rootFolder),
    RequestPath = WorkDirectoryOptions.RequestPath
});


app.UseDefaultFiles();
app.MapFallbackToFile("/client-app/index.html");

app.UseRouting();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
