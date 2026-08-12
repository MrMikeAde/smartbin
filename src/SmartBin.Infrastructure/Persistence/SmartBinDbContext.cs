using Microsoft.EntityFrameworkCore;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Infrastructure.Persistence
{
    public class SmartBinDbContext : DbContext
    {
        public SmartBinDbContext(DbContextOptions<SmartBinDbContext> options) : base(options)
        {
        }

        public DbSet<SmartBinItem> SmartBinItems { get; set; } = null!;
        public DbSet<ActivityLog> ActivityLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SmartBinItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OriginalPath).IsRequired();
                entity.Property(e => e.OriginalFileName).IsRequired();
                entity.Property(e => e.Sha256Hash).IsRequired();

                entity.Property(e => e.CompressionStatus)
                    .HasConversion<int>();

                entity.Property(e => e.CompressionAlgorithm)
                    .HasConversion<int>();

                entity.Property(e => e.RestorationStatus)
                    .HasConversion<int>();
            });

            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Timestamp).IsRequired();
                entity.Property(e => e.OperationType).IsRequired();
                entity.Property(e => e.ResultState).IsRequired();
            });
        }
    }
}
