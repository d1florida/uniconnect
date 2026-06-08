using System.Text.RegularExpressions;

namespace UniConnect.RoutePlanning.Routing;

public readonly record struct AddressGeocodeHints(string? City, string? State, string? PostalCode)
{
    private static readonly Regex ZipRegex = new(@"\b(\d{5})(?:-\d{4})?\b", RegexOptions.Compiled);
    private static readonly Regex TrailingZipRegex = new(@"\b(\d{5})(?:-\d{4})?\s*$", RegexOptions.Compiled);
    private static readonly Regex StateRegex = new(
        @"\b(A[LKSZR]|C[AOT]|D[EC]|F[LM]|G[AU]|HI|I[DLNA]|K[SY]|LA|M[EHDAINSOT]|N[EVHJMYCD]|O[HKR]|P[WAR]|RI|S[CD]|T[XN]|UT|V[TIA]|W[AIVY])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static AddressGeocodeHints Empty => default;

    public static AddressGeocodeHints Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return Empty;

        var trimmed = address.Trim();
        string? postalCode = null;
        Match? zipMatch = TrailingZipRegex.Match(trimmed);
        if (!zipMatch.Success)
            zipMatch = ZipRegex.Matches(trimmed).Cast<Match>().LastOrDefault(m => m.Success);
        if (zipMatch is { Success: true })
            postalCode = zipMatch.Groups[1].Value;

        string? city = null;
        if (postalCode is not null)
        {
            var beforeZip = trimmed[..zipMatch.Index].Trim().TrimEnd(',');
            city = ExtractCityToken(beforeZip);
        }

        string? state = null;
        var stateMatch = StateRegex.Match(trimmed);
        if (stateMatch.Success)
            state = stateMatch.Groups[1].Value.ToUpperInvariant();

        return new AddressGeocodeHints(city, state, postalCode);
    }

    private static string? ExtractCityToken(string beforeZip)
    {
        var tokens = beforeZip.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var candidateTokens = (tokens.Length > 1 ? tokens[^1] : beforeZip)
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (var i = candidateTokens.Length - 1; i >= 0; i--)
        {
            var token = candidateTokens[i];
            if (token.Any(char.IsDigit))
                continue;
            if (IsStreetToken(token))
                continue;
            if (token.Length == 2 && token.All(char.IsLetter))
                continue;
            return TitleCase(token);
        }

        return null;
    }

    public static AddressGeocodeHints Merge(AddressGeocodeHints left, AddressGeocodeHints right) =>
        new(
            left.City ?? right.City,
            left.State ?? right.State,
            left.PostalCode ?? right.PostalCode);

    public static bool IsIncomplete(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return true;

        var hints = Parse(address);
        return hints.PostalCode is null && hints.State is null;
    }

    public bool HasUsContext() =>
        !string.IsNullOrWhiteSpace(PostalCode) || !string.IsNullOrWhiteSpace(State);

    public string Enrich(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return address;

        var trimmed = address.Trim().TrimEnd(',');
        var existing = Parse(trimmed);
        var city = existing.City ?? City;
        var postalCode = existing.PostalCode ?? PostalCode;
        var state = existing.State ?? State ?? InferStateFromPostalCode(postalCode);

        if (city is null && state is null && postalCode is null)
            return trimmed;

        var suffixParts = new List<string>();
        if (city is not null)
            suffixParts.Add(city);
        if (state is not null)
            suffixParts.Add(state);
        if (postalCode is not null)
            suffixParts.Add(postalCode);

        var suffix = string.Join(", ", suffixParts);
        if (trimmed.Contains(suffix, StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return $"{trimmed}, {suffix}";
    }

    private static bool IsStreetToken(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized is "ST" or "STREET" or "LN" or "LANE" or "AVE" or "AVENUE" or "RD" or "ROAD"
            or "BLVD" or "DR" or "DRIVE" or "CT" or "COURT" or "WAY" or "PL" or "PLACE";
    }

    private static string TitleCase(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    public static string? InferStateFromPostalCode(string? postalCode) =>
        postalCode switch
        {
            null => null,
            var p when p.StartsWith("337") => "FL",
            var p when p.StartsWith("941") => "CA",
            var p when p.StartsWith("946") => "CA",
            _ => null,
        };
}
