using System.Text.RegularExpressions;

namespace Cronus.Scripting;

/// <summary>Thrown inside a script's thread to unwind it when the conversation ends.</summary>
public sealed class ConversationEndedException : Exception
{
}

/// <summary>What <see cref="NpcConversation.Advance"/> did with a client answer.</summary>
public enum NpcAnswerResult
{
    /// <summary>The answer matched the pending prompt: the script continues (or the dialog ended normally).</summary>
    Accepted,

    /// <summary>No prompt was pending, or the answer named another message type — ignored.</summary>
    Mismatched,

    /// <summary>
    /// The answer picked an option the prompt never offered. Only a hand-crafted packet does
    /// that; the conversation was ended without running the script any further.
    /// </summary>
    Rejected,
}

/// <summary>
/// The conversation manager exposed to NPC scripts as the global <c>cm</c> (ports
/// <c>OdinNPCConversationManager</c>). Each <c>send*</c>/<c>ask*</c> emits a dialog message and
/// then blocks the script thread until the client answers (matching the upstream Nashorn-on-a-
/// thread model), returning the player's choice so scripts read as linear code.
/// </summary>
public sealed class NpcConversation : IDisposable
{
    /// <summary>A menu option marker, <c>#L&lt;id&gt;#</c>; the id is what the client echoes as its selection.</summary>
    private static readonly Regex MenuOption = new(@"#L(\d+)#", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly INpcDialog _dialog;
    private readonly SemaphoreSlim _answerReady = new(0, 1);
    private readonly int _timeoutMs;

    private volatile bool _ended;
    private int _lastMessageType = -1;
    private HashSet<int>? _offered;
    private int _action;
    private int _selection;
    private string _text = string.Empty;

    public NpcConversation(int npcId, INpcDialog dialog, int timeoutMs = 300_000)
    {
        NpcId = npcId;
        _dialog = dialog;
        _timeoutMs = timeoutMs;
    }

    public int NpcId { get; }

    /// <summary>The message type currently awaiting an answer, or -1 if none.</summary>
    public int PendingMessageType => _lastMessageType;

    /// <summary>True once the script has finished (returned, escaped, or errored).</summary>
    public bool IsEnded => _ended;

    // --- Script-facing API (lowercase to match existing OdinMS-style scripts) ---

    public void sendNext(string text) => Prompt(ScriptMessageType.Say, () => _dialog.Say(NpcId, text, false, true));

    public void sendPrev(string text) => Prompt(ScriptMessageType.Say, () => _dialog.Say(NpcId, text, true, false));

    public void sendNextPrev(string text) => Prompt(ScriptMessageType.Say, () => _dialog.Say(NpcId, text, true, true));

    public void sendOk(string text) => Prompt(ScriptMessageType.Say, () => _dialog.Say(NpcId, text, false, false));

    public bool askYesNo(string text)
    {
        Prompt(ScriptMessageType.AskYesNo, () => _dialog.AskYesNo(NpcId, text));
        return _action == 1;
    }

    public bool askAccept(string text)
    {
        Prompt(ScriptMessageType.AskAccept, () => _dialog.AskAccept(NpcId, text));
        return _action == 1;
    }

    /// <summary>
    /// Shows a menu and returns the id of the option the player picked — always one of the
    /// <c>#L&lt;id&gt;#</c> ids in <paramref name="text"/>. An answer naming any other id ends the
    /// conversation before the script sees it (see <see cref="OfferedSelections"/>).
    /// </summary>
    public int askMenu(string text)
    {
        Prompt(ScriptMessageType.AskMenu, () => _dialog.AskMenu(NpcId, text), OfferedSelections(text));
        return _selection;
    }

    /// <summary>Alias for <see cref="askMenu"/> (OdinMS scripts call this <c>sendSimple</c>).</summary>
    public int sendSimple(string text) => askMenu(text);

    public string askText(string text)
    {
        Prompt(ScriptMessageType.AskText, () => _dialog.AskText(NpcId, text));
        return _text;
    }

    /// <summary>
    /// Shows the style-picker (SM_ASKAVATAR) over the candidate hair/face/skin ids and returns
    /// the chosen index into <paramref name="styles"/>, or -1 if the player cancelled. An index
    /// outside the candidates ends the conversation instead.
    /// </summary>
    public int askAvatar(string text, params int[] styles)
    {
        Prompt(ScriptMessageType.AskAvatar, () => _dialog.AskAvatar(NpcId, text, styles), new HashSet<int>(Enumerable.Range(0, styles.Length)));
        return _action == 0 ? -1 : _selection;
    }

    /// <summary>Opens the janken dialog (ports <c>sendRPS</c>); it runs outside the conversation.</summary>
    public void sendRPS() => _dialog.OpenRps(NpcId);

    public void dispose() => End();

    /// <summary>
    /// The selection ids a menu text offers — every <c>#L&lt;id&gt;#</c> in it. The client sends
    /// its pick back as a plain int, so a hand-crafted answer could otherwise name any value: a
    /// taxi script whose option ids are map ids and which warps to the id it gets back would then
    /// fly anywhere (Riremito's jms_scripts leave exactly that hole open on purpose — the fix is
    /// to check the id against what was offered, which the engine does here for every script).
    /// </summary>
    public static HashSet<int> OfferedSelections(string menuText)
    {
        var ids = new HashSet<int>();
        foreach (Match m in MenuOption.Matches(menuText))
        {
            if (int.TryParse(m.Groups[1].ValueSpan, out int id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    // --- Host-facing side ---

    /// <summary>
    /// Delivers the client's answer, unblocking the script. An escape (<paramref name="action"/>
    /// == -1) or a closed menu ends the conversation; a menu/avatar selection that was never
    /// offered ends it too and is reported as <see cref="NpcAnswerResult.Rejected"/>.
    /// </summary>
    public NpcAnswerResult Advance(int messageType, int action, int selection, string text)
    {
        if (_ended || messageType != _lastMessageType)
        {
            return NpcAnswerResult.Mismatched;
        }

        if (action == -1)
        {
            End();
            return NpcAnswerResult.Accepted;
        }

        // A menu is answered with action 1 and the picked id (the oracle: "JMS is always 1");
        // anything else is the dialog being closed, and the script gets no pick to act on.
        if (messageType == (int)ScriptMessageType.AskMenu && action != 1)
        {
            End();
            return NpcAnswerResult.Accepted;
        }

        if (action != 0 && _offered is not null && !_offered.Contains(selection))
        {
            End();
            return NpcAnswerResult.Rejected;
        }

        _action = action;
        _selection = selection;
        _text = text;
        _lastMessageType = -1;
        _offered = null;
        _answerReady.Release();
        return NpcAnswerResult.Accepted;
    }

    /// <summary>Ends the conversation, unblocking any waiting script thread so it can unwind.</summary>
    public void End()
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        try
        {
            _answerReady.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signaled; fine.
        }
    }

    private void Prompt(ScriptMessageType type, Action send, HashSet<int>? offered = null)
    {
        if (_ended)
        {
            throw new ConversationEndedException();
        }

        _lastMessageType = (int)type;
        _offered = offered;
        send();

        bool answered = _answerReady.Wait(_timeoutMs);
        if (_ended || !answered)
        {
            // Unanswered for the whole window: end here rather than let the script run on with
            // the previous answer still in _selection as if the player had just picked it.
            End();
            throw new ConversationEndedException();
        }
    }

    public void Dispose()
    {
        End();
        _answerReady.Dispose();
    }
}
