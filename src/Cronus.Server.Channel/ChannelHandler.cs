using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;

namespace Cronus.Server.Channel;

/// <summary>
/// Handles channel-stage packets for one connection (ports <c>PacketHandler_Game</c> /
/// <c>ReqCClientSocket.OnMigrateIn</c>, JMS v186 path). On <c>CP_MigrateIn</c>, loads the
/// selected character and replies with <c>LP_SetField</c> to enter the game.
/// </summary>
public sealed class ChannelHandler : PacketHandlerBase
{
    private readonly ChannelPackets _packets;
    private readonly ICharacterRepository _characters;
    private readonly int _channelId;

    private readonly int _opMigrateIn;
    private readonly int _opAliveAck;

    public ChannelHandler(
        OpcodeTable clientOpcodes,
        OpcodeTable serverOpcodes,
        ICharacterRepository characters,
        ServerConfig config,
        int channelId = 0)
    {
        _packets = new ChannelPackets(serverOpcodes, config);
        _characters = characters;
        _channelId = channelId;

        _opMigrateIn = clientOpcodes.Get(ClientOpcode.MigrateIn);
        _opAliveAck = clientOpcodes.Get(ClientOpcode.AliveAck);
    }

    /// <summary>The character bound to this session after a successful migrate-in.</summary>
    public Character? Player { get; private set; }

    public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        if (opcode == _opMigrateIn)
        {
            await HandleMigrateInAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opAliveAck)
        {
            // Keep-alive acknowledged; nothing to do.
        }
    }

    private async ValueTask HandleMigrateInAsync(MapleSession session, PacketReader packet)
    {
        // JMS v186: [characterId:4][machineId:16][unk:2][unk:1][clientKey:8]
        int characterId = packet.ReadInt();

        Character? character = _characters.Find(characterId);
        if (character is null)
        {
            // Unknown character: nothing sensible to enter with. Drop the request.
            return;
        }

        Player = character;
        session.UserData = character;

        (int, int, int) seeds = (RandomSeed(), RandomSeed(), RandomSeed());
        await session.SendAsync(_packets.SetFieldEnterGame(character, _channelId, seeds)).ConfigureAwait(false);
    }

    private static int RandomSeed() => RandomNumberGenerator.GetInt32(int.MaxValue);
}
