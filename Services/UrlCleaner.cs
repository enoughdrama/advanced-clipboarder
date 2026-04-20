namespace Clipboarder.Services;

// Strips well-known tracking parameters from a URL. Conservative by design:
// ambiguous keys (`ref`, `source`, `src`, `s`, `t`, `campaign`) are kept
// because they have legitimate uses on too many sites to blanket-remove.
// Add to TrackerParams if you want a specific vendor covered.
public static class UrlCleaner
{
    private static readonly HashSet<string> TrackerParams = new(StringComparer.OrdinalIgnoreCase)
    {
        // Google Analytics / UTM (industry standard)
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "utm_id",     "utm_name",   "utm_brand",    "utm_social", "utm_social-type",
        // Google Ads / DoubleClick / Search Ads 360
        "gclid", "gclsrc", "dclid", "wbraid", "gbraid", "_gl",
        // Facebook / Meta
        "fbclid", "fb_action_ids", "fb_action_types", "fb_ref",
        // Instagram
        "igshid", "igsh",
        // Microsoft Advertising / Bing
        "msclkid",
        // Yandex
        "yclid", "ysclid",
        // Mailchimp
        "mc_cid", "mc_eid",
        // Marketo
        "mkt_tok",
        // HubSpot
        "_hsenc", "_hsmi", "hsCtaTracking", "hss_channel", "_hsfp",
        // Klaviyo
        "_ke", "_kx",
        // ConvertKit
        "ck_subscriber_id",
        // Vero
        "vero_conv", "vero_id",
        // Pinterest
        "pin_source",
        // Matomo / Piwik
        "piwik_campaign", "piwik_kwd",
        // Generic email tracking
        "oly_anon_id", "oly_enc_id",
    };

    public static string Clean(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return url;
        if (string.IsNullOrEmpty(uri.Query)) return url;

        // Parse manually to preserve the user's original ordering + raw encoding;
        // UriBuilder.Query re-encoding tends to normalize things the user didn't
        // ask us to.
        var kept = new List<string>();
        foreach (var pair in uri.Query.TrimStart('?').Split('&'))
        {
            if (pair.Length == 0) continue;
            var eq = pair.IndexOf('=');
            var key = eq >= 0 ? pair[..eq] : pair;
            if (TrackerParams.Contains(key)) continue;
            kept.Add(pair);
        }

        // If nothing changed, return the original string so the user's chosen
        // formatting (trailing slash, scheme case, etc.) round-trips exactly.
        if (kept.Count == CountPairs(uri.Query)) return url;

        // Rebuild. Drop the '?' entirely if every param was a tracker.
        var prefix = url.Substring(0, url.IndexOf('?'));
        var fragment = uri.Fragment; // includes the '#'
        var queryPart = kept.Count == 0 ? "" : "?" + string.Join("&", kept);
        return prefix + queryPart + fragment;
    }

    private static int CountPairs(string query)
    {
        var n = 0;
        foreach (var pair in query.TrimStart('?').Split('&'))
            if (pair.Length > 0) n++;
        return n;
    }
}
