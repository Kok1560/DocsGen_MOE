namespace DocGen.Data;

public class DocumentShare
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public GeneratedDocument Document { get; set; } = null!;

    public string TokenHash { get; set; } = "";      // SHA-256 of the secret; the secret itself is never stored
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }         // null = never expires
    public bool IsRevoked { get; set; }
}