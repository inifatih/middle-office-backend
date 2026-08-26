using Microsoft.EntityFrameworkCore;
using middle_office_backend.Rmis.Domain.Entities.Auth;
using middle_office_backend.Rmis.Domain.Entities.MiddleOffice;

namespace middle_office_backend.Rmis.Infrastructure.Persistence
{
    public class MioDbContext : DbContext
    {
        public MioDbContext(DbContextOptions<MioDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();

        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<ConfigParameter> ConfigParameters => Set<ConfigParameter>();
        public DbSet<UploadBatch> UploadBatches => Set<UploadBatch>();
        public DbSet<LiquidityDailyReview> LiquidityDailyReviews => Set<LiquidityDailyReview>();
        public DbSet<MaturityProfileHo> MaturityProfileHos => Set<MaturityProfileHo>();
        public DbSet<MaturityProfileKln> MaturityProfileKlns => Set<MaturityProfileKln>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("mio");

            modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(u => u.Username).IsUnique();
                e.Property(u => u.Username).HasMaxLength(100);
                e.Property(u => u.DisplayName).HasMaxLength(200);
            });

            modelBuilder.Entity<Role>(e =>
            {
                e.HasIndex(r => r.Name).IsUnique();
                e.Property(r => r.Name).HasMaxLength(50);
            });

            modelBuilder.Entity<UserRole>(e =>
            {
                e.HasKey(ur => new { ur.UserId, ur.RoleId });
                e.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
                e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
            });

            modelBuilder.Entity<Branch>(e =>
            {
                e.HasIndex(b => b.Code).IsUnique();
                e.Property(b => b.Code).HasMaxLength(10);
                e.Property(b => b.City).HasMaxLength(100);
            });

            modelBuilder.Entity<ConfigParameter>(e =>
            {
                e.HasIndex(c => new { c.Category, c.Key }).IsUnique();
                e.Property(c => c.Category).HasMaxLength(100);
                e.Property(c => c.Key).HasMaxLength(100);
                e.Property(c => c.Value).HasMaxLength(200);
            });

            modelBuilder.Entity<UploadBatch>(e =>
            {
                e.HasIndex(b => new { b.ReportType, b.Period, b.MaturityTipe });
            });

            modelBuilder.Entity<LiquidityDailyReview>(e =>
            {
                e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
                foreach (var prop in e.Metadata.GetProperties())
                {
                    if (prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?))
                        prop.SetColumnType("decimal(20,4)");
                }
            });

            modelBuilder.Entity<MaturityProfileHo>(e =>
            {
                e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
                e.Property(x => x.NilaiIdr).HasColumnType("decimal(20,4)");
                e.Property(x => x.NilaiVa).HasColumnType("decimal(20,4)");
                e.Property(x => x.GapMaturitasIdr).HasColumnType("decimal(20,4)");
                e.Property(x => x.GapMaturitasVa).HasColumnType("decimal(20,4)");
            });

            modelBuilder.Entity<MaturityProfileKln>(e =>
            {
                e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
                e.Property(x => x.Aset).HasColumnType("decimal(20,4)");
                e.Property(x => x.Kewajiban).HasColumnType("decimal(20,4)");
                e.Property(x => x.Selisih).HasColumnType("decimal(20,4)");
                e.Property(x => x.ProfilMaturitasPercent).HasColumnType("decimal(20,4)");
            });

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.Property(a => a.Action).HasMaxLength(50);
                e.Property(a => a.Entity).HasMaxLength(100);
                e.Property(a => a.Username).HasMaxLength(100);
            });

            // Seed data (roles, demo users, config thresholds) lands here once the
            // persistence layer is actually wired up — not used yet in this session.
        }
    }
}
