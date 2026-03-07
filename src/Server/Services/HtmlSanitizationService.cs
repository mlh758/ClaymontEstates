using Ganss.Xss;

namespace Server.Services;

public class HtmlSanitizationService
{
    private readonly HtmlSanitizer _sanitizer = new();

    public HtmlSanitizationService()
    {
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "b", "strong", "i", "em", "u", "a", "ul", "ol", "li" })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] { "href" })
            _sanitizer.AllowedAttributes.Add(attr);

        _sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "https", "mailto" })
            _sanitizer.AllowedSchemes.Add(scheme);

        _sanitizer.AllowedAtRules.Clear();
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
