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

    public NpcScriptEngine(INpcScriptSource scripts) => _scripts = scripts;

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

        var conversation = new NpcConversation(npcId, dialog);
        var thread = new Thread(() => Run(code, conversation, player))
        {
            IsBackground = true,
            Name = $"npc-script-{npcId}",
        };
        thread.Start();
        return conversation;
    }

    private static void Run(string code, NpcConversation conversation, object? player)
    {
        try
        {
            var engine = new Engine(options => options.LimitRecursion(64));
            engine.SetValue("cm", conversation);
            if (player is not null)
            {
                engine.SetValue("player", player);
            }

            engine.Execute(code);
            engine.Invoke("start");
        }
        catch (ConversationEndedException)
        {
            // Normal end (client escaped or dispose() called).
        }
        catch (JavaScriptException)
        {
            // Script bug: end the dialog rather than crash the worker.
        }
        catch (Exception)
        {
            // Any other failure (including a ConversationEndedException wrapped by Jint).
        }
        finally
        {
            conversation.End();
        }
    }
}
