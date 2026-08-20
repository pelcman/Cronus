using System.Net;
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
        PacketWriter w = NewPacket(ServerOpcode.CheckPasswordResult);
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
        PacketWriter w = NewPacket(ServerOpcode.CheckPasswordResult);
        w.WriteByte((byte)result); // result code
        w.WriteByte(result == LoginResult.Blocked ? (byte)32 : (byte)0); // blue-message flag
        return w.ToArray();
    }

    /// <summary>
    /// Builds one <c>LP_WorldInformation</c> entry (JMS v186 layout). Send one per world,
    /// then <see cref="WorldListEnd"/>.
    /// </summary>
    public byte[] WorldInformation(GameWorld world)
    {
        PacketWriter w = NewPacket(ServerOpcode.WorldInformation);
        w.WriteByte((byte)world.Id);            // nWorldID
        w.WriteString(world.Name);              // sName
        w.WriteByte(0);                         // nWorldState
        w.WriteString(world.EventDescription);  // sWorldEventDesc
        w.WriteShort(100);                      // nWorldEventEXP_WSE
        w.WriteShort(100);                      // nWorldEventDrop_WSE
        w.WriteByte((byte)world.Channels.Count);
        foreach (GameChannel channel in world.Channels)
        {
            w.WriteString(channel.Name);        // sName
            w.WriteInt(channel.OnlineCount * 200); // nUserNo
            w.WriteByte((byte)world.Id);        // nWorldID
            w.WriteByte((byte)channel.Id);      // nChannelID
            w.WriteByte(channel.Language);      // bAdultChannel
        }

        w.WriteShort(0);                        // m_nBalloonCount
        return w.ToArray();
    }

    /// <summary>Builds the world-list terminator (<c>nWorldID = -1</c>).</summary>
    public byte[] WorldListEnd()
    {
        PacketWriter w = NewPacket(ServerOpcode.WorldInformation);
        w.WriteByte(0xFF); // nWorldID = -1
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_LatestConnectedWorld</c>.</summary>
    public byte[] LatestConnectedWorld()
    {
        PacketWriter w = NewPacket(ServerOpcode.LatestConnectedWorld);
        w.WriteInt(0); // m_nLatestConnectedWorldID
        return w.ToArray();
    }

    /// <summary>Builds an empty <c>LP_RecommendWorldMessage</c> (no recommendations).</summary>
    public byte[] RecommendWorldMessage()
    {
        PacketWriter w = NewPacket(ServerOpcode.RecommendWorldMessage);
        w.WriteByte(0); // count
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_SelectWorldResult</c> for a successful world selection with an empty
    /// character list (JMS v186 layout). Character serialization arrives with the DB layer.
    /// </summary>
    public byte[] SelectWorldSuccess(int characterSlots)
    {
        PacketWriter w = NewPacket(ServerOpcode.SelectWorldResult);
        w.WriteByte((byte)LoginResult.Success); // result
        w.WriteString(string.Empty);            // JMS marker string
        w.WriteByte(0);                         // character count (empty)
        w.WriteByte(2);                         // m_bLoginOpt (JMS v186 default branch)
        w.WriteByte(0);
        w.WriteInt(characterSlots);             // m_nSlotCount
        w.WriteInt(0);                          // m_nBuyCharCount (JMS >= 186)
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_SelectWorldResult</c> for a failed world selection.</summary>
    public byte[] SelectWorldFailure(LoginResult result)
    {
        PacketWriter w = NewPacket(ServerOpcode.SelectWorldResult);
        w.WriteByte((byte)result);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_SelectCharacterResult</c> — the migrate command handing the client to a
    /// channel server (JMS v186 layout).
    /// </summary>
    public byte[] SelectCharacterResult(IPAddress channelIp, int channelPort, int characterId)
    {
        PacketWriter w = NewPacket(ServerOpcode.SelectCharacterResult);
        w.WriteByte(0);
        w.WriteByte(0);
        w.WriteBytes(channelIp.MapToIPv4().GetAddressBytes()); // sin_addr (4 bytes)
        w.WriteShort(channelPort);                             // sin_port
        w.WriteInt(characterId);                               // m_dwCharacterId
        w.WriteByte(0);
        w.WriteInt(0);
        return w.ToArray();
    }

    /// <summary>Builds the "count related servers" phase of <c>LP_ViewAllCharResult</c>.</summary>
    public byte[] ViewAllCharCount(int worldCount, int characterCount)
    {
        PacketWriter w = NewPacket(ServerOpcode.ViewAllCharResult);
        w.WriteByte(1); // VAC_ResCode_CountRelatedSvrs
        w.WriteInt(worldCount);
        w.WriteInt(characterCount);
        return w.ToArray();
    }

    /// <summary>Builds the success phase of <c>LP_ViewAllCharResult</c> with an empty list.</summary>
    public byte[] ViewAllCharSuccessEmpty()
    {
        PacketWriter w = NewPacket(ServerOpcode.ViewAllCharResult);
        w.WriteByte(0); // VAC_ResCode_Success
        w.WriteByte(0); // m_anWorldID
        w.WriteByte(0); // character count (empty)
        return w.ToArray();
    }

    private PacketWriter NewPacket(string opcodeName)
    {
        int opcode = _serverOps.Get(opcodeName);
        return new PacketWriter(opcode, _config.PacketHeaderSize, _config.CodePage);
    }
}
