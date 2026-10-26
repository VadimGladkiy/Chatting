using Chatting.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatting.Server.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options): DbContext(options)
    {
        public DbSet<Comment> Comments { get; set; }
        public DbSet<CommentImage> CommentImages { get; set; }
        public DbSet<CommentFile> CommentFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Comment>()
                .HasKey(e => e.Id)
                .IsClustered(false); // This removes the clustered index from the Primary Key

            modelBuilder.Entity<Comment>().Property(e => e.UserName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Comment>().Property(e => e.UserEmail)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Comment>()
               .HasOne(b => b.CommentImage)     
               .WithOne(i => i.Comment)           
               .HasForeignKey<CommentImage>(i => i.CommentId)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
               .HasOne(b => b.CommentFile)
               .WithOne(i => i.Comment)
               .HasForeignKey<CommentFile>(i => i.CommentId)
               .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
