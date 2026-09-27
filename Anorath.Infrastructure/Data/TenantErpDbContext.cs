using Anorath.domain.Entities;
using Microsoft.EntityFrameworkCore;
using Anorath.domain.Entities.Reception;
using Anorath.domain.Entities.Operations;

namespace Anorath.Infrastructure.Data
{
    public class TenantErpDbContext : DbContext
    {

        public TenantErpDbContext(
            DbContextOptions<TenantErpDbContext> options)
            : base(options)
        {
        }

        // Tenant-specific business tables

        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<PayrollRecord> PayrollRecords { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<Reservations> Reservations => Set<Reservations>();
        public DbSet<Product> Products => Set<Product>();

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Product
            builder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.ProductId);

                entity.Property(e => e.Price)
                    .HasPrecision(18, 2);
            });

            // Customer
            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.CustomerId);

                entity.Property(e => e.CustomerCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.CustomerName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.ContactNumber)
                    .HasMaxLength(50);

                entity.Property(e => e.EmailAddress)
                    .HasMaxLength(200);

                entity.Property(e => e.Address)
                    .HasMaxLength(500);

                entity.HasIndex(e => e.CustomerCode)
                    .IsUnique();
            });
            // Room
            builder.Entity<Room>(entity =>
            {
                entity.HasKey(e => e.RoomId);

                entity.Property(e => e.RoomNumber)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.RoomType)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.Rate)
                    .HasPrecision(18, 2);

                entity.HasIndex(e => e.RoomNumber)
                    .IsUnique();
            });

            // Reservations
            builder.Entity<Reservations>(entity =>
            {
                entity.HasKey(e => e.ReservationId);

                entity.Property(e => e.Status)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(e => e.Remarks)
                    .HasMaxLength(500);
            });

            // User
            builder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);

                entity.Property(e => e.Username)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.PasswordHash)
                    .IsRequired();

                entity.Property(e => e.FullName)
                    .HasMaxLength(200);

                entity.Property(e => e.Role)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.HasIndex(e => e.Username)
                    .IsUnique();
            });
        }
    }
}