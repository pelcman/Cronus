using Cronus.Common;
using Cronus.Network.Packets;

namespace Cronus.Server.Login;

/// <summary>
/// Builds login-stage server packets for JMS v186 (ports <c>ResCLogin.CheckPasswordResult</c>,
/// JMS branch). The success layout is version-specific; the values encoded here are the exact
/// fields JMS v186 expects — note v186 &lt; 187, so the 2nd-password byte is NOT emitted.
/// </summary>
public sealed class LoginPackets
{
    private readonly OpcodeTable _serverOps;
    private readonly ServerConfig _config;

    public LoginPackets(OpcodeTable serverOpcodes, ServerConfig config)
    {
        _serverOps = serverOpcodes;
        _config = config;
    }

    /// <summary>Builds <c>LP_CheckPasswordResult</c> for a successful login.</summary>
    public byte[] CheckPasswordSuccess(Account account)
    {
        PacketWriter w = NewResult();
        w.WriteByte((byte)LoginResult.Success); // result code
        w.WriteByte(0);                          // OK
        w.WriteInt(account.Id);                  // m_dwAccountId
        w.WriteByte(account.Gender);             // m_nGender
        w.WriteByte(account.IsGameMaster ? (byte)1 : (byte)0); // m_nGradeCode
        w.WriteByte(account.IsGameMaster ? (byte)1 : (byte)0); // JMS >= 164
        w.WriteString(account.LoginId);          // m_sNexonClubID
        w.WriteString(account.LoginId);
        w.WriteByte(0);
        w.WriteByte(0);                          // m_nPurchaseExp
        w.WriteByte(0);                          // m_nChatBlockReason
        w.WriteByte(0);                          // JMS 131..302
        w.WriteByte(0);                          // JMS 164..302
        w.WriteByte(0);                          // JMS 180..302
        // JMS >= 187 would emit a 2nd-password byte here; v186 does not.
        w.WriteLong(0);                          // m_dtChatUnblockDate
        w.WriteString(string.Empty);             // available new-character name (v131 legacy)
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_CheckPasswordResult</c> for a failed login.</summary>
    public byte[] CheckPasswordFailure(LoginResult result)
    {
        PacketWriter w = NewResult();
        w.WriteByte((byte)result); // result code
        w.WriteByte(result == LoginResult.Blocked ? (byte)32 : (byte)0); // blue-message flag
        return w.ToArray();
    }

    private PacketWriter NewResult()
    {
        int opcode = _serverOps.Get(ServerOpcode.CheckPasswordResult);
        return new PacketWriter(opcode, _config.PacketHeaderSize, _config.CodePage);
    }
}
