using System;
using System.Text;
using Lumina.Text;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace Anon;

public static class ChatNames
{
    public static byte[]? Rewrite(ReadOnlySpan<byte> text, Func<string, string, string?> display)
    {
        var builder = new SeStringBuilder();
        var changed = false;
        string? name = null;
        var written = false;
        var replaced = false;

        foreach (var payload in new ReadOnlySeStringSpan(text))
        {
            if (payload.Type == ReadOnlySePayloadType.Macro && payload.MacroCode == MacroCode.Link)
            {
                name = PlayerName(payload);
                written = false;
                replaced = false;
                builder.Append(payload);
                continue;
            }

            if (name == null || payload.Type != ReadOnlySePayloadType.Text)
            {
                builder.Append(payload);
                continue;
            }

            if (written)
            {
                if (!replaced) builder.Append(payload);
                continue;
            }

            written = true;
            var shown = Encoding.UTF8.GetString(payload.Body);
            var target = display(name, shown);
            if (target == null || target == shown)
            {
                builder.Append(payload);
                continue;
            }

            builder.Append(target);
            replaced = true;
            changed = true;
        }

        return changed ? builder.ToArray() : null;
    }

    private static string? PlayerName(ReadOnlySePayloadSpan link)
    {
        if (!link.TryGetExpression(out var kind, out _, out _, out _, out var name)) return null;

        return kind.TryGetUInt(out var type)
            && type == (uint)LinkMacroPayloadType.Character
            && name.TryGetString(out var value)
                ? value.ExtractText()
                : null;
    }
}
