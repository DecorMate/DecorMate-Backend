using DecorMate_Backend_Web_app.Models;
using DecorMateBackend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DecorMate_Backend_Web_app.Data
{
    public class ApplicationDbContext :
        Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<GeneratedImage> GeneratedImages { get; set; } = null!;
        public DbSet<PaymentRecord> PaymentRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Token).IsRequired();
                entity.Property(r => r.ApplicationUserId).IsRequired();

                entity.HasOne(r => r.ApplicationUser)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(r => r.ApplicationUserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
