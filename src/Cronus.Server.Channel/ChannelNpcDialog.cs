using Cronus.Network;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

/// <summary>
/// Bridges the scripting layer's <see cref="INpcDialog"/> to the channel session: each call
/// builds an <c>LP_ScriptMessage</c> and sends it. Called from the script's worker thread, so
/// the async send is awaited synchronously (the worker thread is dedicated to one conversation).
/// </summary>
public sealed class ChannelNpcDialog : INpcDialog
{
    private const int SmSay = 0;
    private const int SmAskYesNo = 2;
    private const int SmAskText = 3;
    private const int SmAskMenu = 5;
    private const int SmAskAccept = 13;

    private readonly MapleSession _session;
    private readonly ChannelPackets _packets;

    public ChannelNpcDialog(MapleSession session, ChannelPackets packets)
    {
        _session = session;
        _packets = packets;
    }

    public void Say(int npcId, string text, bool prev, bool next)
        => Send(_packets.ScriptMessage(npcId, SmSay, text, prev, next));

    public void AskYesNo(int npcId, string text)
        => Send(_packets.ScriptMessage(npcId, SmAskYesNo, text, false, false));

    public void AskMenu(int npcId, string text)
        => Send(_packets.ScriptMessage(npcId, SmAskMenu, text, false, false));

    public void AskText(int npcId, string text)
        => Send(_packets.ScriptMessage(npcId, SmAskText, text, false, false));

    public void AskAccept(int npcId, string text)
        => Send(_packets.ScriptMessage(npcId, SmAskAccept, text, false, false));

    private void Send(byte[] packet)
        => _session.SendAsync(packet).AsTask().GetAwaiter().GetResult();
}
