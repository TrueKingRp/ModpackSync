using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ModpackSync.Server.Data;
using ModpackSync.Server.Repositories;
using ModpackSync.Server.Services;
using ModpackSync.Server.Storage;

var builder =
    WebApplication.CreateBuilder(
        args);

const long maximumUploadSize =
    2L * 1024L * 1024L * 1024L;

string dataDirectory =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "data");

Directory.CreateDirectory(
    dataDirectory);

string databasePath =
    Path.Combine(
        dataDirectory,
        "modpacksync.db");

builder.WebHost.ConfigureKestrel(
    options =>
    {
        options.Limits.MaxRequestBodySize =
            maximumUploadSize;
    });

builder.Services.Configure<FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit =
            maximumUploadSize;
    });

builder.Services
    .AddOptions<BlobStorageOptions>()
    .Bind(
        builder.Configuration.GetSection(
            BlobStorageOptions.SectionName))
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.RootPath),
        "Blob storage root path cannot be empty.")
    .Validate(
        options =>
            options.MaximumFileSizeBytes > 0,
        "Blob storage maximum file size must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddSingleton<
    IBlobStorageService,
    BlobStorageService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<
    ModpackSyncDbContext>(
    options =>
        options.UseSqlite(
            $"Data Source={databasePath}"));

builder.Services.AddScoped<
    IPackRepository,
    PackRepository>();

builder.Services.AddScoped<
    IPackService,
    PackService>();

builder.Services.AddScoped<
    IVersionRepository,
    VersionRepository>();

builder.Services.AddScoped<
    IVersionService,
    VersionService>();

builder.Services.AddScoped<
    IStoredFileRepository,
    StoredFileRepository>();

builder.Services.AddScoped<
    IVersionFileRepository,
    VersionFileRepository>();

builder.Services.AddScoped<
    IVersionFileService,
    VersionFileService>();

builder.Services.AddScoped<
    IVersionManifestService,
    VersionManifestService>();

builder.Services.AddScoped<
    IVersionArchiveService,
    VersionArchiveService>();

builder.Services.AddSingleton<
    IVersionManifestQueue,
    VersionManifestQueue>();

builder.Services.AddHostedService<
    VersionManifestWorker>();

builder.Services.AddControllers();

builder.Services.AddOpenApi();

var app =
    builder.Build();

using (IServiceScope scope =
       app.Services.CreateScope())
{
    ModpackSyncDbContext database =
        scope.ServiceProvider
            .GetRequiredService<
                ModpackSyncDbContext>();

    await database.Database
        .EnsureCreatedAsync();

    IBlobStorageService blobStorage =
        scope.ServiceProvider
            .GetRequiredService<
                IBlobStorageService>();

    app.Logger.LogInformation(
        "Database path: {DatabasePath}",
        databasePath);

    app.Logger.LogInformation(
        "Blob storage directory: {BlobStorageDirectory}",
        blobStorage.RootDirectory);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet(
    "/",
    (
        IBlobStorageService blobStorage) =>
        Results.Ok(
            new
            {
                name =
                    "ModpackSync Server",

                status =
                    "Running",

                database =
                    databasePath,

                storage =
                    blobStorage.RootDirectory
            }));

app.Run();