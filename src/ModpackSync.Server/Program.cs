using Microsoft.EntityFrameworkCore;
using ModpackSync.Server.Data;
using ModpackSync.Server.Repositories;
using ModpackSync.Server.Services;

var builder = WebApplication.CreateBuilder(args);

string dataDirectory = Path.Combine(
    builder.Environment.ContentRootPath,
    "data");

Directory.CreateDirectory(dataDirectory);

string databasePath = Path.Combine(
    dataDirectory,
    "modpacksync.db");

builder.Services.AddDbContext<ModpackSyncDbContext>(
    options =>
        options.UseSqlite(
            $"Data Source={databasePath}"));

builder.Services.AddScoped<
    IPackRepository,
    PackRepository>();

builder.Services.AddScoped<
    IPackService,
    PackService>();

builder.Services.AddControllers();

builder.Services.AddOpenApi();

var app = builder.Build();

using (IServiceScope scope =
       app.Services.CreateScope())
{
    ModpackSyncDbContext database =
        scope.ServiceProvider
            .GetRequiredService<ModpackSyncDbContext>();

    await database.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet(
    "/",
    () => Results.Ok(
        new
        {
            name = "ModpackSync Server",
            status = "Running",
            database = databasePath
        }));

app.Run();