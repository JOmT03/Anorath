using Anorath.domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Anorath.Infrastructure.Data
{
    public class MasterErpDbContext : DbContext
    {
        public MasterErpDbContext(DbContextOptions<MasterErpDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<CompanyDatabase> CompanyDatabases => Set<CompanyDatabase>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<MasterUser> MasterUsers => Set<MasterUser>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Company
            builder.Entity<Company>(entity =>
            {
                entity.HasKey(x => x.CompanyId);
                entity.Property(x => x.CompanyCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Plan).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => x.CompanyCode).IsUnique();
            });

            // CompanyDatabase (one per company; each can be on a different server)
            builder.Entity<CompanyDatabase>(entity =>
            {
                entity.HasKey(x => x.CompanyDatabaseId);
                entity.Property(x => x.ServerName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.DatabaseName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.CredentialKey).HasMaxLength(50);
                entity.HasIndex(x => x.CompanyId).IsUnique();   // one database per company

                entity.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Device
            builder.Entity<Device>(entity =>
            {
                entity.HasKey(x => x.DeviceId);
                entity.Property(x => x.DeviceCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.DeviceName).HasMaxLength(200).IsRequired();

                entity.HasOne(x => x.Company)
                    .WithMany(x => x.Devices)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.CompanyId, x.DeviceCode }).IsUnique();
            });

            // MasterUser (login lives here)
            builder.Entity<MasterUser>(entity =>
            {
                entity.HasKey(x => x.MasterUserId);
                entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
                entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
                entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
                entity.HasIndex(x => x.Username).IsUnique();

                entity.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}