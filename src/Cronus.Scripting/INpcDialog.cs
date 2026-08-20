namespace Cronus.Scripting;

/// <summary>
/// The channel-side surface a running NPC conversation drives: each call builds and sends a
/// <c>LP_ScriptMessage</c> of the given kind to the player. The scripting layer stays free of
/// packet/network knowledge; the channel server implements this.
/// </summary>
public interface INpcDialog
{
    /// <summary>SM_SAY with the given previous/next button visibility.</summary>
    void Say(int npcId, string text, bool prev, bool next);

    /// <summary>SM_ASKYESNO.</summary>
    void AskYesNo(int npcId, string text);

    /// <summary>SM_ASKMENU (text carries <c>#L..#l</c> selection markup).</summary>
    void AskMenu(int npcId, string text);

    /// <summary>SM_ASKTEXT.</summary>
    void AskText(int npcId, string text);

    /// <summary>SM_ASKACCEPT.</summary>
    void AskAccept(int npcId, string text);
}
