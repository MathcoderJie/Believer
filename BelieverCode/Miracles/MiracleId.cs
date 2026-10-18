using System.Diagnostics.CodeAnalysis;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Stable, unique identifier of a 神迹 / Miracle.
///
/// The value is the miracle card's <c>ModelId.Entry</c> (for example
/// <c>BELIEVER-DEV_MIRACLE_TEST_CARD</c>). BaseLib prefixes mod model ids with the mod id, and the
/// entry is derived from the card class name, so as long as a miracle card keeps its class name its
/// id stays stable across saves, sessions and platforms. This is what gets persisted in the run save,
/// so renaming a miracle card is a save-breaking change by design.
///
/// M4 intentionally ships only the id wrapper; the 12 real miracle cards will each supply their id
/// through <see cref="FromCard"/> once they exist.
/// </summary>
public readonly struct MiracleId : IEquatable<MiracleId>, IComparable<MiracleId>
{
    /// <summary>Ids are model entry names; anything much longer than this is not a real id.</summary>
    private const int MaxLength = 128;

    /// <summary>The raw model entry, or an empty string for <c>default</c>.</summary>
    public string Value { get; }

    /// <summary>True for <c>default(MiracleId)</c> and for ids created from empty input.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    private MiracleId(string value) => Value = value;

    /// <summary>
    /// Creates a well-formed id from raw text. This only validates the shape (non-empty, bounded,
    /// no whitespace); whether a miracle with that id actually exists is checked separately against
    /// <see cref="MiracleCatalog"/>, which is what makes unknown ids safe to handle.
    /// </summary>
    public static bool TryCreate(string? raw, out MiracleId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength) return false;
        if (trimmed.IndexOfAny([' ', '\t', '\r', '\n']) >= 0) return false;

        id = new MiracleId(trimmed);
        return true;
    }

    /// <summary>The stable id of an existing miracle card.</summary>
    public static MiracleId FromCard(MiracleCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new MiracleId(card.Id.Entry);
    }

    public bool Equals(MiracleId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is MiracleId other && Equals(other);

    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(MiracleId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value ?? string.Empty;

    public static bool operator ==(MiracleId left, MiracleId right) => left.Equals(right);

    public static bool operator !=(MiracleId left, MiracleId right) => !left.Equals(right);
}
