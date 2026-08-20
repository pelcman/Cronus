namespace Cronus.Scripting;

/// <summary>
/// NPC script message kinds (ports <c>OpsScriptMan</c>). The numeric values are the wire
/// <c>nMsgType</c> the client echoes back in its answer.
/// </summary>
public enum ScriptMessageType
{
    Say = 0,
    SayImage = 1,
    AskYesNo = 2,
    AskText = 3,
    AskNumber = 4,
    AskMenu = 5,
    AskQuiz = 6,
    AskSpeedQuiz = 7,
    AskAvatar = 8,
    AskAccept = 13,
    AskBoxText = 14,
    AskSlideMenu = 15,
}
