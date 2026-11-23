using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocGen.Services;

public partial class DocxEngine
{
    // Matches {{Name}}, {{ Name }}, {{salary_2}} ...
    [GeneratedRegex(@"\{\{\s*([\p{L}_][\p{L}\p{N}_]*)\s*\}\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>Returns the unique field names found in the template, in order of appearance.</summary>
    public IReadOnlyList<string> DetectFields(string docxPath)
    {
        using var stream = new FileStream(docxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var doc = WordprocessingDocument.Open(stream, false);

        var fields = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // {{age}} and {{Age}} = same field

        foreach (var root in GetContentRoots(doc))
        {
            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                var text = GetParagraphText(paragraph);
                foreach (Match match in PlaceholderRegex().Matches(text))
                {
                    var name = match.Groups[1].Value;
                    if (seen.Add(name)) fields.Add(name);
                }
            }
        }

        return fields;
    }

    /// <summary>
    /// Creates a NEW document in memory with the placeholders replaced.
    /// The template file on disk is only read, never changed.
    /// </summary>
    public byte[] Fill(string templatePath, IReadOnlyDictionary<string, string> values)
    {
        var lookup = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

        using var memory = new MemoryStream();
        using (var source = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            source.CopyTo(memory);
        }

        using (var doc = WordprocessingDocument.Open(memory, true))
        {
            foreach (var root in GetContentRoots(doc))
            {
                foreach (var paragraph in root.Descendants<Paragraph>())
                {
                    ReplaceInParagraph(paragraph, lookup);
                }
            }
        } // disposing the document saves the changes into the memory stream

        return memory.ToArray();
    }

    private static void ReplaceInParagraph(Paragraph paragraph, Dictionary<string, string> values)
    {
        var texts = GetTexts(paragraph);
        if (texts.Count == 0) return;

        var full = string.Concat(texts.Select(t => t.Text));
        var matches = PlaceholderRegex().Matches(full);
        if (matches.Count == 0) return;

        // owner[i] = which text piece character i of the joined string belongs to
        var owner = new int[full.Length];
        var pos = 0;
        for (var t = 0; t < texts.Count; t++)
            for (var c = 0; c < texts[t].Text.Length; c++)
                owner[pos++] = t;

        var builders = texts.Select(_ => new StringBuilder()).ToArray();
        var i = 0;
        foreach (Match m in matches)
        {
            // Text before the placeholder stays in its original piece
            for (; i < m.Index; i++) builders[owner[i]].Append(full[i]);

            // The value goes into the piece where the placeholder starts
            builders[owner[m.Index]].Append(values.TryGetValue(m.Groups[1].Value, out var v) ? v : "");

            // The rest of the placeholder is skipped (this removes it)
            i = m.Index + m.Length;
        }
        for (; i < full.Length; i++) builders[owner[i]].Append(full[i]);

        for (var t = 0; t < texts.Count; t++)
        {
            texts[t].Text = builders[t].ToString();
            texts[t].Space = SpaceProcessingModeValues.Preserve; // keep leading/trailing spaces
        }
    }

    /// <summary>Every part of the document that can contain text: body, headers, footers.</summary>
    internal static List<OpenXmlElement> GetContentRoots(WordprocessingDocument doc)
    {
        var roots = new List<OpenXmlElement>();
        var main = doc.MainDocumentPart;
        if (main is null) return roots;

        if (main.Document?.Body is { } body) roots.Add(body);
        roots.AddRange(main.HeaderParts.Where(h => h.Header is not null).Select(h => (OpenXmlElement)h.Header));
        roots.AddRange(main.FooterParts.Where(f => f.Footer is not null).Select(f => (OpenXmlElement)f.Footer));
        return roots;
    }

    /// <summary>The text pieces that belong directly to this paragraph (not to a paragraph nested inside it).</summary>
    private static List<Text> GetTexts(Paragraph paragraph) =>
        paragraph.Descendants<Text>()
            .Where(t => t.Ancestors<Paragraph>().First() == paragraph)
            .ToList();

    private static string GetParagraphText(Paragraph paragraph) =>
        string.Concat(GetTexts(paragraph).Select(t => t.Text));
}