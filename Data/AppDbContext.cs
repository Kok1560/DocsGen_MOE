using Microsoft.EntityFrameworkCore;

namespace DocGen.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<GeneratedDocument> Documents => Set<GeneratedDocument>();
    public DbSet<DocumentShare> Shares => Set<DocumentShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();   // no two users with the same name
        });

        modelBuilder.Entity<GeneratedDocument>(e =>
        {
            e.ToTable("Documents");
            e.Property(d => d.Name).HasMaxLength(200).IsRequired();
            e.Property(d => d.TemplateName).HasMaxLength(200).IsRequired();
            e.Property(d => d.DocxKey).IsRequired();
            e.Property(d => d.PdfKey).IsRequired();

            // Deleting a user deletes their document records
            e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);

            // Makes "my documents, newest first" fast
            e.HasIndex(d => new { d.UserId, d.CreatedAt });
        });

        modelBuilder.Entity<DocumentShare>(e =>
        {
            e.ToTable("Shares");
            e.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(s => s.TokenHash).IsUnique();      // also makes the link lookup fast
            e.HasIndex(s => s.DocumentId);

            // Deleting a document deletes its share links
            e.HasOne(s => s.Document).WithMany().HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}