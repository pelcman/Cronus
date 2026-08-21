using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class BuffTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void FromSpec_BuildsAscendingBitStats_AndMask()
    {
        var spec = new ConsumeSpec { ItemId = 2012000, Pad = 8, Speed = 6, Time = 300000 };
        List<BuffStat> stats = BuffEffect.FromSpec(spec);

        Assert.Equal(2, stats.Count);
        Assert.Equal(0, stats[0].Bit);   // PAD first (bit 0)
        Assert.Equal((short)8, stats[0].Value);
        Assert.Equal(7, stats[1].Bit);   // Speed second (bit 7)
        Assert.Equal((short)6, stats[1].Value);
        Assert.Equal(0x81u, BuffEffect.Word0Mask(stats)); // PAD|Speed
    }

    [Fact]
    public void FromSpec_NoDuration_IsEmpty()
    {
        Assert.Empty(BuffEffect.FromSpec(new ConsumeSpec { ItemId = 2000000, Pad = 8, Time = 0 }));
    }

    [Fact]
    public void TemporaryStatSet_MatchesReferenceLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var spec = new ConsumeSpec { ItemId = 2012000, Pad = 8, Speed = 6, Time = 300000 };

        var r = new PacketReader(packets.TemporaryStatSet(BuffEffect.FromSpec(spec)), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(0, r.ReadInt());          // mask word[3]
        Assert.Equal(0, r.ReadInt());          // word[2]
        Assert.Equal(0, r.ReadInt());          // word[1]
        Assert.Equal(0x81, r.ReadInt());       // word[0] = PAD|Speed
        // PAD entry
        Assert.Equal((short)8, r.ReadShort());
        Assert.Equal(-2012000, r.ReadInt());   // reason = -itemId
        Assert.Equal(300000, r.ReadInt());     // duration ms
        // Speed entry
        Assert.Equal((short)6, r.ReadShort());
        Assert.Equal(-2012000, r.ReadInt());
        Assert.Equal(300000, r.ReadInt());
        // Tail: nDefenseAtt, nDefenseState, delay(2), changed-point
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.ReadShort());
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void TemporaryStatReset_IsMaskPlusTrailer()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.TemporaryStatReset(0x81), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(0, r.ReadInt());
        Assert.Equal(0, r.ReadInt());
        Assert.Equal(0, r.ReadInt());
        Assert.Equal(0x81, r.ReadInt());
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.Remaining);
    }

    /// <summary>Migrates in, drinks a buff potion, flags when the buff packet arrives.</summary>
    private sealed class Drinker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _potionId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opBuff = ServerOps.Get(ServerOpcode.TemporaryStatSet);
        private bool _sent;

        public Drinker(int characterId, int potionId)
        {
            _characterId = characterId;
            _potionId = potionId;
        }

        public TaskCompletionSource<int> Buffed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserStatChangeItemUseRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);        // timestamp
                w.WriteShort(1);      // USE slot 1
                w.WriteInt(_potionId);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opBuff)
            {
                p.ReadInt(); p.ReadInt(); p.ReadInt(); // mask words 3,2,1
                Buffed.TrySetResult(p.ReadInt());      // word[0]
            }
        }
    }

    [Fact]
    public async Task DrinkingBuffPotion_SendsBuffAndConsumesIt()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Brawler", MapId = 100000000 });
        hero.EquippedItems.Add(new InventoryItem { ItemId = 2012000, Position = 1, Quantity = 1, CharacterId = hero.Id });
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var items = new InMemoryItemProvider(new[] { new ConsumeSpec { ItemId = 2012000, Pad = 8, Time = 300000 } });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Drinker(hero.Id, 2012000);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, items: items);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int mask = await client.Buffed.Task.WaitAsync(cts.Token);

        Assert.Equal(0x1, mask); // PAD bit
        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 2012000); // potion consumed
    }
}
