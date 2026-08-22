using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Login;
using Xunit;

namespace Cronus.Server.Login.Tests;

/// <summary>
/// Drives the character lifecycle through the encrypted wire: login -> select world (empty) ->
/// check name -> create character -> select world again (one character), decoding the full
/// JMS v186 GW_CharacterStat + AvatarLook layout on the way back.
/// </summary>
public class CharacterFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed record DecodedCharacter(
        int Id, string Name, byte Level, short Job, short Str, short Hp, int Face, int Hair);

    private sealed class FlowClient : PacketHandlerBase
    {
        private readonly int _opCheckPwResult = ServerOps.Get(ServerOpcode.CheckPasswordResult);
        private readonly int _opDupIdResult = ServerOps.Get(ServerOpcode.CheckDuplicatedIdResult);
        private readonly int _opCreateResult = ServerOps.Get(ServerOpcode.CreateNewCharacterResult);
        private readonly int _opSelectWorldResult = ServerOps.Get(ServerOpcode.SelectWorldResult);

        private int _selectWorldRound;

        public TaskCompletionSource<bool> NameAvailable { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<DecodedCharacter> Created { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<List<DecodedCharacter>> SecondWorldList { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = NewPacket(session, ClientOpcode.CheckPassword);
            w.WriteString("charuser");
            w.WriteString("pw");
            w.WriteBytes(new byte[16]);
            w.WriteInt(0);
            w.WriteByte(0);
            w.WriteByte(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
        {
            if (opcode == _opCheckPwResult)
            {
                Assert.Equal((int)LoginResult.Success, packet.ReadByte());
                await SelectWorldAsync(session);
            }
            else if (opcode == _opSelectWorldResult)
            {
                _selectWorldRound++;
                packet.ReadByte();       // result
                packet.ReadString();     // JMS marker
                int count = packet.ReadByte();

                var chars = new List<DecodedCharacter>();
                for (int i = 0; i < count; i++)
                {
                    chars.Add(DecodeCharacterEntry(packet));
                }

                if (_selectWorldRound == 1)
                {
                    Assert.Empty(chars);
                    // Ask whether the name is free before creating.
                    var w = NewPacket(session, ClientOpcode.CheckDuplicatedId);
                    w.WriteString("Kaede");
                    await session.SendAsync(w.ToArray());
                }
                else
                {
                    SecondWorldList.TrySetResult(chars);
                }
            }
            else if (opcode == _opDupIdResult)
            {
                packet.ReadString();     // echoed name
                bool available = packet.ReadByte() == 0;
                NameAvailable.TrySetResult(available);

                // JMS v186 CP_CreateNewCharacter layout.
                var w = NewPacket(session, ClientOpcode.CreateNewCharacter);
                w.WriteString("Kaede");
                w.WriteInt(1);           // job type (pre-BB: 0 = Cygnus, 1 = Adventurer, 2 = Aran)
                w.WriteShort(0);         // job sub-type
                w.WriteInt(20000);       // face
                w.WriteInt(30000);       // hair
                w.WriteInt(1040002);     // top
                w.WriteInt(1060002);     // bottom
                w.WriteInt(1072001);     // shoes
                w.WriteInt(1302000);     // weapon
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opCreateResult)
            {
                Assert.Equal((int)LoginResult.Success, packet.ReadByte());
                Created.TrySetResult(DecodeStatAndLook(packet));
                // Re-select the world; the new character must appear.
                await SelectWorldAsync(session);
            }
        }

        private static async ValueTask SelectWorldAsync(MapleSession session)
        {
            var w = NewPacket(session, ClientOpcode.SelectWorld);
            w.WriteByte(0);
            w.WriteByte(0);
            await session.SendAsync(w.ToArray());
        }

        private static PacketWriter NewPacket(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);

        // Decodes GW_CharacterStat + AvatarLook + family byte + ranking (select-world entry).
        private static DecodedCharacter DecodeCharacterEntry(PacketReader p)
        {
            DecodedCharacter c = DecodeStatAndLook(p);
            p.ReadByte();                 // family byte (select-world only)
            byte hasRanking = p.ReadByte();
            Assert.Equal(1, hasRanking);
            p.Skip(16);                   // 4x ranking ints
            return c;
        }

        private static DecodedCharacter DecodeStatAndLook(PacketReader p)
        {
            // --- GW_CharacterStat (JMS v186) ---
            int id = p.ReadInt();
            string name = p.ReadFixedString(13);
            p.ReadByte();                 // gender
            p.ReadByte();                 // skin
            int face = p.ReadInt();
            int hair = p.ReadInt();
            p.Skip(24);                   // pet/reserved block
            byte level = p.ReadByte();
            short job = p.ReadShort();
            short str = p.ReadShort();
            p.ReadShort();                // dex
            p.ReadShort();                // int
            p.ReadShort();                // luk
            short hp = p.ReadShort();
            p.ReadShort();                // maxhp
            p.ReadShort();                // mp
            p.ReadShort();                // maxmp
            p.ReadShort();                // ap
            p.ReadShort();                // sp
            p.ReadInt();                  // exp
            p.ReadShort();                // fame
            p.ReadInt();                  // gasha exp
            p.ReadInt();                  // map
            p.ReadByte();                 // portal
            p.ReadShort();                // subcategory
            p.Skip(8 + 4 + 4 + 4);        // JMS pre-BB tail

            // --- AvatarLook (JMS v186) ---
            p.ReadByte();                 // gender
            p.ReadByte();                 // skin
            p.ReadInt();                  // face
            p.ReadByte();                 // ignored
            p.ReadInt();                  // hair
            while (p.ReadByte() != 0xFF)  // visible equips: [slot][itemId] until 0xFF
            {
                p.ReadInt();
            }

            Assert.Equal(0xFF, p.ReadByte()); // masked equips terminator
            p.ReadInt();                  // weapon sticker
            p.ReadInt();                  // pet 1
            p.ReadLong();                 // pets 2-3

            return new DecodedCharacter(id, name, level, job, str, hp, face, hair);
        }
    }

    [Fact]
    public async Task CreateCharacter_ThenItAppearsInWorldSelect()
    {
        var config = ServerConfig.Jms186;
        var loginService = new LoginService(new InMemoryAccountRepository());
        var characterRepo = new InMemoryCharacterRepository();
        var client = new FlowClient();

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server,
            new LoginHandler(ClientOps, ServerOps, loginService, config, characters: characterRepo));
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        bool nameAvailable = await client.NameAvailable.Task.WaitAsync(cts.Token);
        DecodedCharacter created = await client.Created.Task.WaitAsync(cts.Token);
        List<DecodedCharacter> list = await client.SecondWorldList.Task.WaitAsync(cts.Token);

        Assert.True(nameAvailable);

        Assert.Equal("Kaede", created.Name);
        Assert.Equal(1, created.Level);
        Assert.Equal(0, created.Job);
        Assert.Equal(12, created.Str);
        Assert.Equal(50, created.Hp);
        Assert.Equal(20000, created.Face);
        Assert.Equal(30000, created.Hair);

        DecodedCharacter listed = Assert.Single(list);
        Assert.Equal(created.Id, listed.Id);
        Assert.Equal("Kaede", listed.Name);

        // Repository actually persisted it.
        Assert.True(characterRepo.NameExists("kaede")); // case-insensitive
    }
}
