using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// Drives game entry through the encrypted wire: the client connects to the channel session,
/// sends CP_MigrateIn with a character id, and decodes the LP_SetField reply — walking the
/// full JMS v186 CharacterData layout field by field so a byte shift anywhere fails loudly.
/// </summary>
public class MigrateInFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed record SetFieldResult(
        int ChannelId, bool CharacterData, int CharacterId, string Name, short Job, int MapId, long ServerTime);

    private sealed class MigratingClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);

        public MigratingClient(int characterId) => _characterId = characterId;

        public TaskCompletionSource<SetFieldResult> EnteredGame { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            // JMS v186 CP_MigrateIn: [characterId:4][machineId:16][unk:2][unk:1][clientKey:8]
            var w = new PacketWriter(
                ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode != _opSetField)
            {
                return ValueTask.CompletedTask;
            }

            // --- SetField header (JMS v186) ---
            Assert.Equal(0, p.ReadShort());        // ClientOptMan: no entries
            int channelId = p.ReadInt();
            p.ReadByte();                          // JMS >= 146 byte
            p.ReadInt();                           // old driver id
            p.ReadByte();                          // portal count
            bool characterData = p.ReadByte() == 1;
            Assert.Equal(0, p.ReadShort());        // notifier check
            p.Skip(12);                            // 3 damage seeds

            // --- CharacterData(all) ---
            Assert.Equal(-1L, p.ReadLong());       // statmask
            p.ReadByte();                          // combat orders

            // GW_CharacterStat
            int characterId = p.ReadInt();
            string name = p.ReadFixedString(13);
            p.Skip(1 + 1 + 4 + 4 + 24);            // gender/skin/face/hair/reserved
            p.ReadByte();                          // level
            short job = p.ReadShort();
            p.Skip(8);                             // str/dex/int/luk
            p.Skip(8);                             // hp/maxhp/mp/maxmp (16-bit each)
            p.Skip(4);                             // ap/sp
            p.ReadInt();                           // exp
            p.ReadShort();                         // fame
            p.ReadInt();                           // gasha exp
            int mapId = p.ReadInt();
            p.ReadByte();                          // portal
            p.ReadShort();                         // subcategory
            p.Skip(8 + 4 + 4 + 4);                 // pre-BB tail

            p.ReadByte();                          // buddy capacity
            Assert.Equal(0, p.ReadByte());         // bless of fairy: none

            p.ReadInt();                           // meso
            Assert.Equal(characterId, p.ReadInt()); // pachinko: character id
            p.Skip(8);                             // pachinko tama + reserved

            // InventoryInfo (empty)
            p.Skip(5);                             // slot limits
            p.Skip(8);                             // 0x100000 pair
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(0, p.ReadShort());    // equip-section terminators
            }

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(0, p.ReadByte());     // use/setup/etc/cash terminators
            }

            Assert.Equal(0, p.ReadShort());        // skills
            Assert.Equal(0, p.ReadShort());        // cooldowns
            Assert.Equal(0, p.ReadShort());        // started quests
            Assert.Equal(0, p.ReadShort());        // JMS 184-186 extra
            Assert.Equal(0, p.ReadShort());        // completed quests
            Assert.Equal(0, p.ReadShort());        // minigame
            Assert.Equal(0, p.ReadShort());        // couple rings
            Assert.Equal(0, p.ReadShort());        // friend rings
            Assert.Equal(0, p.ReadShort());        // marriage records
            for (int i = 0; i < 15; i++)
            {
                Assert.Equal(999999999, p.ReadInt()); // teleport rocks
            }

            Assert.Equal(0, p.ReadShort());        // presents
            p.ReadInt();                           // monster book cover
            Assert.Equal(0, p.ReadByte());         // monster book not shrunk
            Assert.Equal(0, p.ReadShort());        // card count
            Assert.Equal(0, p.ReadShort());        // quest info records
            Assert.Equal(0, p.ReadShort());        // pre-BB 0x80000
            Assert.Equal(0, p.ReadShort());        // visitor quest log

            p.Skip(16);                            // logout gift config (4 ints)
            long serverTime = p.ReadLong();        // ftServer
            Assert.Equal(0, p.Remaining);          // nothing left over — exact layout

            EnteredGame.TrySetResult(new SetFieldResult(channelId, characterData, characterId, name, job, mapId, serverTime));
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task MigrateIn_RepliesWithFullSetField()
    {
        var config = ServerConfig.Jms186;
        var characters = new InMemoryCharacterRepository();
        Character hero = characters.Create(new Character
        {
            AccountId = 1,
            WorldId = 0,
            Name = "Kaede",
            Face = 20000,
            Hair = 30000,
            MapId = 100000000,
        });

        var client = new MigratingClient(hero.Id);
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var handler = new ChannelHandler(ClientOps, ServerOps, characters, config, channelId: 0);
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        SetFieldResult result = await client.EnteredGame.Task.WaitAsync(cts.Token);

        Assert.Equal(0, result.ChannelId);
        Assert.True(result.CharacterData);
        Assert.Equal(hero.Id, result.CharacterId);
        Assert.Equal("Kaede", result.Name);
        Assert.Equal(0, result.Job);
        Assert.Equal(100000000, result.MapId);
        Assert.True(result.ServerTime > 0);
        Assert.Same(hero, handler.Player);
    }

    [Fact]
    public async Task MigrateIn_UnknownCharacter_SendsNothing()
    {
        var config = ServerConfig.Jms186;
        var characters = new InMemoryCharacterRepository();

        var client = new MigratingClient(characterId: 999);
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var handler = new ChannelHandler(ClientOps, ServerOps, characters, config);
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.EnteredGame.Task.WaitAsync(cts.Token));
        Assert.Null(handler.Player);
    }
}
