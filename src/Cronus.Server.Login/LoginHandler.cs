using System.Net;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;

namespace Cronus.Server.Login;

/// <summary>
/// Handles login-stage packets for one connection (ports <c>PacketHandler_Login</c> +
/// <c>ReqCLogin</c>, JMS v186 path): credential check, world list, world/channel select,
/// character list, name-duplication check, character creation, and character select (migrate).
/// </summary>
public sealed class LoginHandler : PacketHandlerBase
{
    private const int MinNameLength = 4;
    private const int MaxNameLength = 12;

    private readonly LoginService _loginService;
    private readonly LoginPackets _packets;
    private readonly WorldRegistry _worlds;
    private readonly ICharacterRepository _characters;
    private readonly IPEndPoint _channelEndpoint;

    /// <summary>Every channel's endpoint (index = channel id); character select routes by choice.</summary>
    private readonly IReadOnlyList<IPEndPoint> _channelEndpoints;
    private readonly int _characterSlots;
    private readonly int _startMapId;

    private readonly int _opCheckPassword;
    private readonly int _opGetMapLogin;
    private readonly int _opWorldInfoRequest;
    private readonly int _opSelectWorld;
    private readonly int _opViewAllChar;
    private readonly int _opCheckDuplicatedId;
    private readonly int _opCreateNewCharacter;
    private readonly int _opDeleteCharacter;
    private readonly int _opSelectCharacter;

    public LoginHandler(
        OpcodeTable clientOpcodes,
        OpcodeTable serverOpcodes,
        LoginService loginService,
        ServerConfig config,
        WorldRegistry? worlds = null,
        ICharacterRepository? characters = null,
        IPEndPoint? channelEndpoint = null,
        int characterSlots = 3,
        int startMapId = 100000000,
        IReadOnlyList<IPEndPoint>? channelEndpoints = null)
    {
        _loginService = loginService;
        _packets = new LoginPackets(serverOpcodes, config);
        _worlds = worlds ?? WorldRegistry.CreateDefault();
        _characters = characters ?? new InMemoryCharacterRepository();
        _channelEndpoint = channelEndpoint ?? new IPEndPoint(IPAddress.Loopback, 7575);
        _channelEndpoints = channelEndpoints is { Count: > 0 } ? channelEndpoints : new[] { _channelEndpoint };
        _characterSlots = characterSlots;
        _startMapId = startMapId;

        _opCheckPassword = clientOpcodes.Get(ClientOpcode.CheckPassword);
        _opGetMapLogin = clientOpcodes.Get(ClientOpcode.JmsGetMapLogin);
        _opWorldInfoRequest = clientOpcodes.Get(ClientOpcode.WorldInfoRequest);
        _opSelectWorld = clientOpcodes.Get(ClientOpcode.SelectWorld);
        _opViewAllChar = clientOpcodes.Get(ClientOpcode.ViewAllChar);
        _opCheckDuplicatedId = clientOpcodes.Get(ClientOpcode.CheckDuplicatedId);
        _opCreateNewCharacter = clientOpcodes.Get(ClientOpcode.CreateNewCharacter);
        _opDeleteCharacter = clientOpcodes.Get(ClientOpcode.DeleteCharacter);
        _opSelectCharacter = clientOpcodes.Get(ClientOpcode.SelectCharacter);
    }

    public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        if (opcode == _opCheckPassword)
        {
            await HandleCheckPasswordAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opGetMapLogin)
        {
            // The JMS login screen is a rendered map; the client asks which one to show.
            await session.SendAsync(_packets.SetMapLogin()).ConfigureAwait(false);
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
        else if (opcode == _opCheckDuplicatedId)
        {
            await HandleCheckDuplicatedIdAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opCreateNewCharacter)
        {
            await HandleCreateNewCharacterAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opDeleteCharacter)
        {
            await HandleDeleteCharacterAsync(session, packet).ConfigureAwait(false);
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

        byte[] response = outcome is { Result: LoginResult.Success, Account: { } account }
            ? _packets.CheckPasswordSuccess(account)
            : _packets.CheckPasswordFailure(outcome.Result);

        if (outcome.Account is not null)
        {
            session.UserData = new LoginState { Account = outcome.Account };
        }

        await session.SendAsync(response).ConfigureAwait(false);

        // JMS v186 pushes the world list immediately after a successful login (registerClient ->
        // OnWorldInfoRequest); the client waits for it rather than requesting it.
        if (outcome.Result == LoginResult.Success)
        {
            await HandleWorldInfoRequestAsync(session).ConfigureAwait(false);
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
        if (world is null || session.UserData is not LoginState state)
        {
            await session.SendAsync(_packets.SelectWorldFailure(LoginResult.NotConnectableWorld)).ConfigureAwait(false);
            return;
        }

        state.SelectedWorld = worldId;
        state.SelectedChannel = channelId;

        IReadOnlyList<Character> characters = _characters.ListByAccount(state.Account.Id, worldId);
        await session.SendAsync(_packets.SelectWorldSuccess(characters, _characterSlots)).ConfigureAwait(false);
    }

    private async ValueTask HandleViewAllCharAsync(MapleSession session)
    {
        IReadOnlyList<Character> characters = session.UserData is LoginState state
            ? _characters.ListByAccount(state.Account.Id, worldId: 0)
            : Array.Empty<Character>();

        await session.SendAsync(_packets.ViewAllCharCount(_worlds.Worlds.Count, characters.Count)).ConfigureAwait(false);
        await session.SendAsync(_packets.ViewAllCharSuccess(worldId: 0, characters)).ConfigureAwait(false);
    }

    private async ValueTask HandleCheckDuplicatedIdAsync(MapleSession session, PacketReader packet)
    {
        string name = packet.ReadString();
        bool available = IsNameValid(name) && !_characters.NameExists(name);
        await session.SendAsync(_packets.CheckDuplicatedIdResult(name, available)).ConfigureAwait(false);
    }

    private async ValueTask HandleCreateNewCharacterAsync(MapleSession session, PacketReader packet)
    {
        if (session.UserData is not LoginState state)
        {
            await session.SendAsync(_packets.CreateNewCharacterFailure(LoginResult.Unknown)).ConfigureAwait(false);
            return;
        }

        // JMS v186 CP_CreateNewCharacter:
        //   [name:str][jobType:int][jobDualblade:short][face:int][hair:int]
        //   [top:int][bottom:int][shoes:int][weapon:int]
        string name = packet.ReadString();
        packet.ReadInt();           // job type (0 = Adventurers for v186)
        packet.ReadShort();         // job sub-type (dual blade / cannoneer)
        int face = packet.ReadInt();
        int hair = packet.ReadInt();
        int top = packet.ReadInt();
        int bottom = packet.ReadInt();
        int shoes = packet.ReadInt();
        int weapon = packet.ReadInt();

        if (!IsNameValid(name))
        {
            await session.SendAsync(_packets.CreateNewCharacterFailure(LoginResult.InvalidCharacterName)).ConfigureAwait(false);
            return;
        }

        if (_characters.NameExists(name))
        {
            await session.SendAsync(_packets.CreateNewCharacterFailure(LoginResult.InvalidCharacterName)).ConfigureAwait(false);
            return;
        }

        var character = new Character
        {
            AccountId = state.Account.Id,
            WorldId = Math.Max(state.SelectedWorld, 0),
            Name = name,
            Gender = state.Account.Gender,
            SkinColor = 0,
            Face = face,
            Hair = hair,
            Level = 1,
            Job = 0,
            Str = 12,
            Dex = 5,
            Int = 4,
            Luk = 4,
            MapId = _startMapId,
        };

        AddStarterEquips(character, top, bottom, shoes, weapon);

        Character created = _characters.Create(character);
        await session.SendAsync(_packets.CreateNewCharacterSuccess(created)).ConfigureAwait(false);
    }

    private async ValueTask HandleDeleteCharacterAsync(MapleSession session, PacketReader packet)
    {
        // JMS v186 CP_DeleteCharacter: [characterId:4] (the MapleID prefix arrives at JMS >= 188).
        int characterId = packet.ReadInt();

        // Only allow deleting a character that belongs to the logged-in account.
        bool owned = session.UserData is LoginState state
            && _characters.Find(characterId) is { } c
            && c.AccountId == state.Account.Id;

        bool success = owned && _characters.Delete(characterId);
        await session.SendAsync(_packets.DeleteCharacterResult(characterId, success)).ConfigureAwait(false);
    }

    private async ValueTask HandleSelectCharacterAsync(MapleSession session, PacketReader packet)
    {
        int characterId = packet.ReadInt();

        // Route to the channel picked at world select (clamped; defaults to channel 0).
        int channel = session.UserData is LoginState state
            ? Math.Clamp(state.SelectedChannel, 0, _channelEndpoints.Count - 1)
            : 0;
        IPEndPoint endpoint = _channelEndpoints[channel];
        byte[] migrate = _packets.SelectCharacterResult(endpoint.Address, endpoint.Port, characterId);
        await session.SendAsync(migrate).ConfigureAwait(false);
    }

    /// <summary>Places the starter equipment the client sent at the standard equip slots.</summary>
    private static void AddStarterEquips(Character character, int top, int bottom, int shoes, int weapon)
    {
        // Standard equip slots (negative positions): top -5, bottom -6, shoes -7, weapon -11.
        AddEquip(character, top, -5);
        AddEquip(character, bottom, -6);
        AddEquip(character, shoes, -7);
        AddEquip(character, weapon, -11);
    }

    private static void AddEquip(Character character, int itemId, short position)
    {
        if (itemId <= 0)
        {
            return; // e.g. a one-piece overall omits the bottom slot
        }

        character.EquippedItems.Add(new InventoryItem { ItemId = itemId, Position = position });
    }

    private static bool IsNameValid(string name)
        => name.Length is >= MinNameLength and <= MaxNameLength;
}
