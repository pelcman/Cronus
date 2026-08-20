namespace Cronus.Scripting;

/// <summary>Thrown inside a script's thread to unwind it when the conversation ends.</summary>
public sealed class ConversationEndedException : Exception
{
}

/// <summary>
/// The conversation manager exposed to NPC scripts as the global <c>cm</c> (ports
/// <c>OdinNPCConversationManager</c>). Each <c>send*</c>/<c>ask*</c> emits a dialog message and
/// then blocks the script thread until the client answers (matching the upstream Nashorn-on-a-
/// thread model), returning the player's choice so scripts read as linear code.
/// </summary>
public sealed class NpcConversation : IDisposable
{
    private readonly INpcDialog _dialog;
    private readonly SemaphoreSlim _answerReady = new(0, 1);
    private readonly int _timeoutMs;

    private volatile bool _ended;
    private int _lastMessageType = -1;
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

    public int askMenu(string text)
    {
        Prompt(ScriptMessageType.AskMenu, () => _dialog.AskMenu(NpcId, text));
        return _selection;
    }

    /// <summary>Alias for <see cref="askMenu"/> (OdinMS scripts call this <c>sendSimple</c>).</summary>
    public int sendSimple(string text) => askMenu(text);

    public string askText(string text)
    {
        Prompt(ScriptMessageType.AskText, () => _dialog.AskText(NpcId, text));
        return _text;
    }

    public void dispose() => End();

    // --- Host-facing side ---

    /// <summary>
    /// Delivers the client's answer, unblocking the script. Returns false if it does not match
    /// the pending prompt. An escape (<paramref name="action"/> == -1) ends the conversation.
    /// </summary>
    public bool Advance(int messageType, int action, int selection, string text)
    {
        if (_ended || messageType != _lastMessageType)
        {
            return false;
        }

        if (action == -1)
        {
            End();
            return true;
        }

        _action = action;
        _selection = selection;
        _text = text;
        _lastMessageType = -1;
        _answerReady.Release();
        return true;
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

    private void Prompt(ScriptMessageType type, Action send)
    {
        if (_ended)
        {
            throw new ConversationEndedException();
        }

        _lastMessageType = (int)type;
        send();

        _answerReady.Wait(_timeoutMs);
        if (_ended)
        {
            throw new ConversationEndedException();
        }
    }

    public void Dispose()
    {
        End();
        _answerReady.Dispose();
    }
}
