using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Data;

public sealed class ModpackSyncDbContext : DbContext
{
    public ModpackSyncDbContext(
        DbContextOptions<ModpackSyncDbContext> options)
        : base(options)
    {
    }

    public DbSet<ServerPack> Packs =>
        Set<ServerPack>();

    public DbSet<ServerPackVersion> Versions =>
        Set<ServerPackVersion>();

    public DbSet<StoredFile> StoredFiles =>
        Set<StoredFile>();

    public DbSet<VersionFile> VersionFiles =>
        Set<VersionFile>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerPack>(entity =>
        {
            entity.HasKey(pack => pack.Id);

            entity.Property(pack => pack.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(pack => pack.Name)
                .IsUnique();
        });

        modelBuilder.Entity<ServerPackVersion>(entity =>
        {
            entity.HasKey(version => version.Id);

            entity.Property(version => version.VersionLabel)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(version => version.Pack)
                .WithMany(pack => pack.Versions)
                .HasForeignKey(version => version.PackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(version => new
            {
                version.PackId,
                version.VersionLabel
            })
            .IsUnique();
        });

        modelBuilder.Entity<StoredFile>(entity =>
        {
            entity.HasKey(file => file.Sha256);

            entity.Property(file => file.Sha256)
                .HasMaxLength(64);

            entity.Property(file => file.StoragePath)
                .IsRequired();
        });

        modelBuilder.Entity<VersionFile>(entity =>
        {
            entity.HasKey(file => new
            {
                file.VersionId,
                file.RelativePath
            });

            entity.Property(file => file.RelativePath)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(file => file.Sha256)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasOne(file => file.Version)
                .WithMany(version => version.Files)
                .HasForeignKey(file => file.VersionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(file => file.StoredFile)
                .WithMany(storedFile => storedFile.VersionFiles)
                .HasForeignKey(file => file.Sha256)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}