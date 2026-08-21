using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class LootMessageTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void IncExpMessage_StartsWithTypeColorAndExp()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.IncExpMessage(1234), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.Message), r.ReadHeader());
        Assert.Equal(3, r.ReadByte());     // MS_IncEXPMessage
        Assert.Equal(0, r.ReadByte());     // text colour
        Assert.Equal(1234, r.ReadInt());   // gained exp
        Assert.Equal(0, r.ReadByte());     // on-quest
        // remaining bonus fields are all zero
        Assert.Equal(0, r.ReadInt());
        Assert.Equal(0, r.ReadByte());     // mob-event bonus %
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.ReadInt());      // wedding bonus
        Assert.Equal(0, r.ReadInt());      // group ring bonus
        Assert.Equal(0, r.ReadByte());     // party bonus rate
        Assert.Equal(0, r.ReadInt());      // party bonus
        Assert.Equal(0, r.ReadInt());      // equip bonus
        Assert.Equal(0, r.ReadInt());
        Assert.Equal(0, r.ReadInt());      // rainbow bonus
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void IncMoneyMessage_IsTypeAndAmount()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.IncMoneyMessage(500), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(6, r.ReadByte());     // MS_IncMoneyMessage
        Assert.Equal(500, r.ReadInt());
        Assert.Equal(0, r.Remaining);
    }

    private sealed class Killer : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMobEnter = ServerOps.Get(ServerOpcode.MobEnterField);
        private readonly int _opMessage = ServerOps.Get(ServerOpcode.Message);
        private int _mobOid = -1;
        private bool _setField;
        private bool _attacked;

        public Killer(int characterId) => _characterId = characterId;

        public TaskCompletionSource<int> ExpMessage { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                _setField = true;
                await MaybeAttack(session);
            }
            else if (opcode == _opMobEnter)
            {
                _mobOid = p.ReadInt();
                await MaybeAttack(session);
            }
            else if (opcode == _opMessage && p.ReadByte() == 3) // MS_IncEXPMessage
            {
                p.ReadByte();          // colour
                ExpMessage.TrySetResult(p.ReadInt());
            }
        }

        private async ValueTask MaybeAttack(MapleSession session)
        {
            if (_setField && _mobOid >= 0 && !_attacked)
            {
                _attacked = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMeleeAttack), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);
                w.WriteInt(0);
                w.WriteInt(0);
                w.WriteByte(0x11);     // 1 damage, 1 mob
                w.WriteInt(0);
                w.WriteInt(0);
                w.WriteInt(0);
                w.WriteInt(0);
                w.WriteInt(0);
                w.WriteInt(0);
                w.WriteByte(0);
                w.WriteShort(0);
                w.WriteByte(0);
                w.WriteByte(0);
                w.WriteInt(0);
                w.WriteInt(0);
                w.WriteInt(_mobOid);
                w.WriteBytes(new byte[4]);
                w.WriteBytes(new byte[8]);
                w.WriteShort(0);
                w.WriteInt(100);       // lethal (mob has 50 HP)
                w.WriteInt(0);
                await session.SendAsync(w.ToArray());
            }
        }
    }

    [Fact]
    public async Task Kill_SendsExpGainMessage()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hunter", MapId = 100000000, Level = 20 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 50 } },
        };
        var mobData = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 42 } });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobData);

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Killer(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int expShown = await client.ExpMessage.Task.WaitAsync(cts.Token);
        Assert.Equal(42, expShown); // the mob's exp appears as the "+N exp" message
    }
}
