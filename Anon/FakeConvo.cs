using System;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Anon;

public static unsafe class FakeConvo
{
    private const string PrintSourceSignature =
        "40 55 53 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 ?? ?? ?? ?? ?? ?? ?? 45 0F B7 61 1C 45 33 F6";

    private static delegate* unmanaged<RaptureLogModule*, Utf8String*, Utf8String*, ChatSource*, uint> printSource;
    private static bool printSourceResolved;

    public static bool Print(byte logKind, string sender, string message)
    {
        if (message.Length == 0) return false;

        var log = RaptureLogModule.Instance();
        if (log == null) return false;

        if (RaptureLogModule.MemberFunctionPointers.PrintMessage == null) return false;

        var senderName = Utf8String.FromString(sender);
        var text = Utf8String.FromString(message);
        try
        {
            var info = new LogInfo { LogKind = (byte)(logKind & 0x7F) };
            log->PrintMessage(info, senderName, text, (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
            return true;
        }
        finally
        {
            senderName->Dtor(true);
            text->Dtor(true);
        }
    }

    public static bool PrintNumbered(byte logKind, int memberNumber, string sender, string message)
    {
        if (memberNumber is < 1 or > 24 || message.Length == 0) return false;

        var log = RaptureLogModule.Instance();
        if (log == null) return false;

        if (!printSourceResolved)
        {
            printSourceResolved = true;
            try
            {
                if (Plugin.SigScanner.TryScanText(PrintSourceSignature, out var address))
                    printSource = (delegate* unmanaged<RaptureLogModule*, Utf8String*, Utf8String*, ChatSource*, uint>)address;
            }
            catch
            {
                printSource = null;
            }
        }

        if (printSource == null) return false;

        Utf8String* senderName;
        if (logKind == 14)
        {
            var glyph = Encoding.UTF8.GetBytes(((char)(0xE0E0 + memberNumber)).ToString());
            var name = Encoding.UTF8.GetBytes(sender);
            byte[] bytes =
            [
                0x02, 0x13, 0x03, 0xE9, 0x64, 0x03,
                .. glyph,
                0x02, 0x13, 0x03, 0xE9, 0x65, 0x03, 0x02, 0x13, 0x02, 0xEC, 0x03,
                .. name,
                0x02, 0x13, 0x02, 0xEC, 0x03,
            ];
            senderName = Utf8String.FromSequence(bytes);
        }
        else
        {
            senderName = Utf8String.FromString(sender);
        }
        var text = Utf8String.FromString(message);
        try
        {
            var source = new ChatSource
            {
                Timestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ChatType = logKind == 15 ? (ushort)0x23 : (ushort)0x0E,
                PartyOrAllianceMemberIdent = (sbyte)(memberNumber - 1),
            };

            var index = printSource(log, senderName, text, &source);
            if (RaptureLogModule.MemberFunctionPointers.AddMsgSourceEntry != null)
                log->AddMsgSourceEntry(source.ContentId, source.AccountId, (int)index, source.WorldId, source.ChatType);
            return true;
        }
        finally
        {
            senderName->Dtor(true);
            text->Dtor(true);
        }
    }

    public static string NoBreak(string name) => name.Replace(' ', '\u00A0');

    public static string? LastStored()
    {
        var log = RaptureLogModule.Instance();
        if (log == null) return null;

        var count = log->LogMessageCount;
        if (count <= 0) return null;

        var info = default(LogInfo);
        var timestamp = 0;
        var sender = Utf8String.CreateEmpty();
        var message = Utf8String.CreateEmpty();
        try
        {
            if (!log->GetLogMessageDetail(count - 1, &info, sender, message, &timestamp)) return null;
            return $"kind {info.LogKind} | '{sender->ToString()}' | '{message->ToString()}'";
        }
        finally
        {
            sender->Dtor(true);
            message->Dtor(true);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x28)]
    private struct ChatSource
    {
        [FieldOffset(0x00)] public ulong AccountId;
        [FieldOffset(0x08)] public ulong ContentId;
        [FieldOffset(0x10)] public uint Unknown10;
        [FieldOffset(0x14)] public int Timestamp;
        [FieldOffset(0x18)] public uint EntityId;
        [FieldOffset(0x1C)] public ushort ChatType;
        [FieldOffset(0x1E)] public ushort WorldId;
        [FieldOffset(0x20)] public sbyte PartyOrAllianceMemberIdent;
        [FieldOffset(0x21)] public byte Flags;
        [FieldOffset(0x22)] public byte Flags2;
        [FieldOffset(0x23)] public byte Flags3;
        [FieldOffset(0x24)] public uint Unknown24;
    }

}
