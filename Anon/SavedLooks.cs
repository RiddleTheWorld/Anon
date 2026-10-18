using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Anon;

public static class SavedLooks
{
    public const int Size = 26;
    public const int GlamSlots = 10;
    public const int ExtraValues = 9;
    private const int GlamSize = GlamSlots * 8;
    private const int EntrySize = Size + GlamSize;
    private const int FullSize = EntrySize + (ExtraValues * 8);
    private const int FlaggedSize = FullSize + 1;
    private const int DualSize = FullSize * 2;
    private const byte AsSeenFlag = 1;

    public static string Encode(ReadOnlySpan<byte> look, ulong[]? glam = null, ulong[]? extras = null, bool asSeen = false)
    {
        if (glam == null) return Convert.ToBase64String(look);

        var bytes = new byte[extras == null ? EntrySize : FlaggedSize];
        look.CopyTo(bytes);
        WriteValues(bytes, Size, glam, GlamSlots);
        if (extras != null)
        {
            WriteValues(bytes, EntrySize, extras, ExtraValues);
            bytes[FullSize] = asSeen ? AsSeenFlag : (byte)0;
        }

        return Convert.ToBase64String(bytes);
    }

    public static string EncodeBoth(
        ReadOnlySpan<byte> realLook, ulong[] realGlam, ulong[] realExtras,
        ReadOnlySpan<byte> seenLook, ulong[] seenGlam, ulong[] seenExtras)
    {
        var bytes = new byte[DualSize];
        WriteBlock(bytes, 0, realLook, realGlam, realExtras);
        WriteBlock(bytes, FullSize, seenLook, seenGlam, seenExtras);
        return Convert.ToBase64String(bytes);
    }

    private static void WriteBlock(byte[] bytes, int offset, ReadOnlySpan<byte> look, ulong[] glam, ulong[] extras)
    {
        look.CopyTo(bytes.AsSpan(offset, Size));
        WriteValues(bytes, offset + Size, glam, GlamSlots);
        WriteValues(bytes, offset + EntrySize, extras, ExtraValues);
    }

    public readonly record struct Shape(bool Both, bool AsSeen, bool Glam, bool Extras);

    public static Shape? Describe(string? stored)
    {
        if (Bytes(stored) is not { } bytes) return null;
        if (bytes.Length is not (Size or EntrySize or FullSize or FlaggedSize or DualSize)) return null;

        return new Shape(
            Both: bytes.Length == DualSize,
            AsSeen: bytes.Length == FlaggedSize && (bytes[FullSize] & AsSeenFlag) != 0,
            Glam: bytes.Length != Size,
            Extras: bytes.Length is FullSize or FlaggedSize or DualSize);
    }

    public static bool HasBoth(Dictionary<string, string> looks, string label) =>
        looks.TryGetValue(label, out var stored) && Describe(stored) is { Both: true };

    public static bool IsAsSeen(Dictionary<string, string> looks, string label) =>
        looks.TryGetValue(label, out var stored) && Describe(stored) is { AsSeen: true };

    private static byte[]? Bytes(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return null;

        try
        {
            return Convert.FromBase64String(stored);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static bool TryDecodeEntry(string? stored, out byte[] look, out ulong[]? glam) =>
        TryDecodeEntry(stored, out look, out glam, out _);

    public static bool TryDecodeEntry(string? stored, out byte[] look, out ulong[]? glam, out ulong[]? extras) =>
        TryDecodeEntry(stored, false, out look, out glam, out extras);

    public static bool TryDecodeEntry(string? stored, bool asSeen, out byte[] look, out ulong[]? glam, out ulong[]? extras)
    {
        look = [];
        glam = null;
        extras = null;
        if (Bytes(stored) is not { } decoded) return false;

        if (decoded.Length == DualSize)
        {
            var offset = asSeen ? FullSize : 0;
            look = decoded[offset..(offset + Size)];
            glam = ReadValues(decoded, offset + Size, GlamSlots);
            extras = ReadValues(decoded, offset + EntrySize, ExtraValues);
            return true;
        }

        if (decoded.Length == Size)
        {
            look = decoded;
            return true;
        }

        if (decoded.Length is not (EntrySize or FullSize or FlaggedSize)) return false;

        look = decoded[..Size];
        glam = ReadValues(decoded, Size, GlamSlots);
        if (decoded.Length >= FullSize) extras = ReadValues(decoded, EntrySize, ExtraValues);

        return true;
    }

    private static void WriteValues(byte[] bytes, int offset, ulong[] values, int count)
    {
        for (var i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset + (i * 8)), values[i]);
        }
    }

    private static ulong[] ReadValues(byte[] bytes, int offset, int count)
    {
        var values = new ulong[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset + (i * 8)));
        }

        return values;
    }

    public static bool HasGlam(Dictionary<string, string> looks, string label) =>
        looks.TryGetValue(label, out var stored) && TryDecodeEntry(stored, out _, out var glam) && glam != null;

    public static bool HasExtras(Dictionary<string, string> looks, string label) =>
        looks.TryGetValue(label, out var stored) && TryDecodeEntry(stored, out _, out _, out var extras) && extras != null;

    public static bool TryDecode(string? stored, out byte[] look) => TryDecodeEntry(stored, out look, out _);

    public static byte[]? For(Dictionary<string, string> assignments, Dictionary<string, string> looks, string name)
    {
        if (name.Length == 0 || !assignments.TryGetValue(name, out var label)) return null;

        return TryDecode(looks.TryGetValue(label, out var stored) ? stored : null, out var look) ? look : null;
    }

    public static void Rename(Dictionary<string, string> looks, Dictionary<string, string> assignments, string from, string to)
    {
        if (from == to || !looks.Remove(from, out var stored)) return;

        looks[to] = stored;
        foreach (var (player, label) in new List<KeyValuePair<string, string>>(assignments))
        {
            if (label == from) assignments[player] = to;
        }
    }

    public static void Remove(Dictionary<string, string> looks, Dictionary<string, string> assignments, string label)
    {
        looks.Remove(label);

        foreach (var (player, assigned) in new List<KeyValuePair<string, string>>(assignments))
        {
            if (assigned == label) assignments.Remove(player);
        }
    }

    public static string FreeLabel(Dictionary<string, string> looks, string wanted)
    {
        wanted = wanted.Trim();
        if (wanted.Length == 0) wanted = "Look";
        if (!looks.ContainsKey(wanted)) return wanted;

        for (var n = 2; ; n++)
        {
            var candidate = $"{wanted} {n}";
            if (!looks.ContainsKey(candidate)) return candidate;
        }
    }

    public static IEnumerable<string> UsedBy(Dictionary<string, string> assignments, string label)
    {
        foreach (var (player, assigned) in assignments)
        {
            if (assigned == label) yield return player;
        }
    }
}
