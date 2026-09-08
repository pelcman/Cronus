using Jint;
using Jint.Runtime;

namespace Cronus.Scripting;

/// <summary>
/// Runs NPC conversation scripts with Jint (ports the role of <c>TacosScriptNPC</c>, replacing
/// Nashorn). A script defines <c>function start() { ... }</c> and drives the dialog through the
/// global <c>cm</c> (an <see cref="NpcConversation"/>) and <c>player</c>. Because <c>cm</c>
/// methods block until the client answers, each conversation runs on its own background thread.
/// </summary>
public sealed class NpcScriptEngine
{
    private readonly INpcScriptSource _scripts;
    private readonly INpcScriptSource? _questScripts;
    private readonly int _answerTimeoutMs;

    /// <param name="answerTimeoutMs">How long a prompt waits for the client's answer before the
    /// conversation is ended (the upstream default is five minutes).</param>
    public NpcScriptEngine(INpcScriptSource scripts, INpcScriptSource? questScripts = null, int answerTimeoutMs = 300_000)
    {
        _scripts = scripts;
        _questScripts = questScripts;
        _answerTimeoutMs = answerTimeoutMs;
    }

    /// <summary>Every NPC id that has a script (for /sweep npcs and the exerciser).</summary>
    public IEnumerable<int> ScriptedNpcIds => _scripts.Ids();

    /// <summary>
    /// Starts the script for <paramref name="npcId"/>, returning the live conversation (whose
    /// answers the caller routes via <see cref="NpcConversation.Advance"/>), or null if the NPC
    /// has no script.
    /// </summary>
    public NpcConversation? Start(int npcId, INpcDialog dialog, object? player = null)
    {
        string? code = _scripts.GetScript(npcId);
        if (code is null)
        {
            return null;
        }

        var conversation = new NpcConversation(npcId, dialog, _answerTimeoutMs);
        var thread = new Thread(() => Run(code, conversation, player, "cm", "start"))
        {
            IsBackground = true,
            Name = $"npc-script-{npcId}",
        };
        thread.Start();
        return conversation;
    }

    /// <summary>
    /// Starts the quest script for <paramref name="questId"/> (ports <c>TacosScriptQuest</c>:
    /// scripts keyed by quest id, bound as the global <c>qm</c>, with <c>start()</c> run for a
    /// script-driven quest opening and <c>end()</c> for a script-driven completion). Returns the
    /// live conversation, or null when the quest has no script — the caller then falls back to the
    /// data-driven accept/complete path.
    /// </summary>
    public NpcConversation? StartQuest(int questId, int npcId, INpcDialog dialog, object? player = null, bool ending = false)
    {
        string? code = _questScripts?.GetScript(questId);
        if (code is null)
        {
            return null;
        }

        var conversation = new NpcConversation(npcId, dialog, _answerTimeoutMs);
        var thread = new Thread(() => Run(code, conversation, player, "qm", ending ? "end" : "start"))
        {
            IsBackground = true,
            Name = $"quest-script-{questId}",
        };
        thread.Start();
        return conversation;
    }

    private static void Run(string code, NpcConversation conversation, object? player, string binding, string entry)
    {
        try
        {
            var engine = new Engine(options => options.LimitRecursion(64));
            engine.SetValue(binding, conversation);
            if (player is not null)
            {
                engine.SetValue("player", player);
            }

            engine.Execute(code);
            engine.Invoke(entry);
        }
        catch (ConversationEndedException)
        {
            // Normal end (client escaped or dispose() called).
        }
        catch (Exception ex) when (ex is ConversationEndedException || ex.InnerException is ConversationEndedException)
        {
            // The same normal end, wrapped by Jint.
        }
        catch (JavaScriptException ex)
        {
            // Script bug: end the dialog rather than crash the worker — but never silently.
            conversation.Error = ex;
            Console.WriteLine($"[script] {binding} {conversation.NpcId} {entry}(): {ex.Message} (line {ex.Location.Start.Line})");
        }
        catch (Exception ex)
        {
            conversation.Error = ex;
            Console.WriteLine($"[script] {binding} {conversation.NpcId} {entry}(): {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            conversation.End();
        }
    }
}
