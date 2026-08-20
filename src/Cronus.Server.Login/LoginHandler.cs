using System.Net;
using Cronus.Common;
using Cronus.Network;
using Cronus.Network.Packets;

namespace Cronus.Server.Login;

/// <summary>
/// Handles login-stage packets for one connection (ports <c>PacketHandler_Login</c> +
/// <c>ReqCLogin</c>, JMS v186 path): credential check, world list, world/channel select,
/// the "view all characters" button, and character select (migrate to a channel server).
/// </summary>
public sealed class LoginHandler : PacketHandlerBase
{
    private readonly LoginService _loginService;
    private readonly LoginPackets _packets;
    private readonly WorldRegistry _worlds;
    private readonly IPEndPoint _channelEndpoint;
    private readonly int _characterSlots;

    private readonly int _opCheckPassword;
    private readonly int _opWorldInfoRequest;
    private readonly int _opSelectWorld;
    private readonly int _opViewAllChar;
    private readonly int _opSelectCharacter;

    public LoginHandler(
        OpcodeTable clientOpcodes,
        OpcodeTable serverOpcodes,
        LoginService loginService,
        ServerConfig config,
        WorldRegistry? worlds = null,
        IPEndPoint? channelEndpoint = null,
        int characterSlots = 3)
    {
        _loginService = loginService;
        _packets = new LoginPackets(serverOpcodes, config);
        _worlds = worlds ?? WorldRegistry.CreateDefault();
        _channelEndpoint = channelEndpoint ?? new IPEndPoint(IPAddress.Loopback, 7575);
        _characterSlots = characterSlots;

        _opCheckPassword = clientOpcodes.Get(ClientOpcode.CheckPassword);
        _opWorldInfoRequest = clientOpcodes.Get(ClientOpcode.WorldInfoRequest);
        _opSelectWorld = clientOpcodes.Get(ClientOpcode.SelectWorld);
        _opViewAllChar = clientOpcodes.Get(ClientOpcode.ViewAllChar);
        _opSelectCharacter = clientOpcodes.Get(ClientOpcode.SelectCharacter);
    }

    public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        if (opcode == _opCheckPassword)
        {
            await HandleCheckPasswordAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opWorldInfoRequest)
        {
            await HandleWorldInfoRequestAsync(session).ConfigureAwait(false);
        }
        else if (opcode == _opSelectWorld)
        {
            await HandleSelectWorldAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opViewAllChar)
        {
            await HandleViewAllCharAsync(session).ConfigureAwait(false);
        }
        else if (opcode == _opSelectCharacter)
        {
            await HandleSelectCharacterAsync(session, packet).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleCheckPasswordAsync(MapleSession session, PacketReader packet)
    {
        // JMS v131..302: [mapleId:str][password:str][machineId:16][unk1:int][unk2][unk3]
        string mapleId = packet.ReadString();
        string password = packet.ReadString();

        LoginService.Outcome outcome = _loginService.Authenticate(mapleId, password);

        if (outcome is { Result: LoginResult.Success, Account: { } account })
        {
            session.UserData = new LoginState { Account = account };
            await session.SendAsync(_packets.CheckPasswordSuccess(account)).ConfigureAwait(false);
        }
        else
        {
            await session.SendAsync(_packets.CheckPasswordFailure(outcome.Result)).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleWorldInfoRequestAsync(MapleSession session)
    {
        foreach (GameWorld world in _worlds.Worlds)
        {
            await session.SendAsync(_packets.WorldInformation(world)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.WorldListEnd()).ConfigureAwait(false);
        await session.SendAsync(_packets.RecommendWorldMessage()).ConfigureAwait(false);
        await session.SendAsync(_packets.LatestConnectedWorld()).ConfigureAwait(false);
    }

    private async ValueTask HandleSelectWorldAsync(MapleSession session, PacketReader packet)
    {
        // JMS v186: [worldId:1][channelId:1]
        int worldId = packet.ReadByte();
        int channelId = packet.ReadByte();

        GameWorld? world = _worlds.Find(worldId);
        if (world is null)
        {
            await session.SendAsync(_packets.SelectWorldFailure(LoginResult.NotConnectableWorld)).ConfigureAwait(false);
            return;
        }

        if (session.UserData is LoginState state)
        {
            state.SelectedWorld = worldId;
            state.SelectedChannel = channelId;
        }

        await session.SendAsync(_packets.SelectWorldSuccess(_characterSlots)).ConfigureAwait(false);
    }

    private async ValueTask HandleViewAllCharAsync(MapleSession session)
    {
        await session.SendAsync(_packets.ViewAllCharCount(_worlds.Worlds.Count, 0)).ConfigureAwait(false);
        await session.SendAsync(_packets.ViewAllCharSuccessEmpty()).ConfigureAwait(false);
    }

    private async ValueTask HandleSelectCharacterAsync(MapleSession session, PacketReader packet)
    {
        int characterId = packet.ReadInt();

        // No characters exist yet (creation lands with the DB layer); this wires the migrate
        // packet so the flow is complete once characters can be created.
        byte[] migrate = _packets.SelectCharacterResult(
            _channelEndpoint.Address, _channelEndpoint.Port, characterId);
        await session.SendAsync(migrate).ConfigureAwait(false);
    }
}
