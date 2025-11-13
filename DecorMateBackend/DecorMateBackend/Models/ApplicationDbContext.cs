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
        public DbSet<VendorRating> VendorRatings { get; set; }
        public DbSet<EmailConfirmation> EmailConfirmations { get; set; } = null!;


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

            builder.Entity<EmailConfirmation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ConfirmationGuid).IsUnique();
                entity.Property(e => e.Token).IsRequired();
                entity.Property(e => e.ApplicationUserId).IsRequired();
                entity.Property(e => e.ConfirmationGuid).IsRequired();

                entity.HasOne(e => e.ApplicationUser)
                      .WithMany()
                      .HasForeignKey(e => e.ApplicationUserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
