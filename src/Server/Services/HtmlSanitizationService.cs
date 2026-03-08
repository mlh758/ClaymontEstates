using Ganss.Xss;

namespace Server.Services;

public class HtmlSanitizationService
{
    private readonly HtmlSanitizer _sanitizer = new();

    public HtmlSanitizationService()
    {
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "b", "strong", "i", "em", "u", "s", "a", "ul", "ol", "li", "div", "span" })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] { "href", "style" })
            _sanitizer.AllowedAttributes.Add(attr);

        _sanitizer.AllowedCssProperties.Clear();
        foreach (var prop in new[] { "font-weight", "font-style", "text-decoration", "color" })
            _sanitizer.AllowedCssProperties.Add(prop);

        _sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "https", "mailto" })
            _sanitizer.AllowedSchemes.Add(scheme);

        _sanitizer.AllowedAtRules.Clear();
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
