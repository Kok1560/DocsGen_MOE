namespace DocGen.Data;

public class GeneratedDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = "";           // the name the user chose
    public string TemplateName { get; set; } = "";
    public string DocxKey { get; set; } = "";        // where the Word file is in the file store
    public string PdfKey { get; set; } = "";         // where the PDF is in the file store
    public string ValuesJson { get; set; } = "{}";   // the field values used
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}