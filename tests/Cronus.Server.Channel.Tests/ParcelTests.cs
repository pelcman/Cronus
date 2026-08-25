using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// The 宅配 (home delivery) flow. The CP_UserParcelRequest SEND payload here is the exact byte
/// layout captured from the live JMS v186 client (2026-08-26 wire log):
/// [action:1][tab:1][slot:2][qty:2][meso:4][recipient:str][flag:1].
/// </summary>
public class ParcelTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class ParcelClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly Action<ParcelClient, MapleSession>? _onEnter;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opParcel = ServerOps.Get("LP_Parcel");
        private bool _entered;

        public ParcelClient(int characterId, Action<ParcelClient, MapleSession>? onEnter = null)
        {
            _characterId = characterId;
            _onEnter = onEnter;
        }

        public MapleSession? Session { get; private set; }

        public TaskCompletionSource<byte> ParcelResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_entered)
            {
                _entered = true;
                _onEnter?.Invoke(this, session);
            }
            else if (opcode == _opParcel)
            {
                ParcelResult.TrySetResult(p.ReadByte());
            }

            return ValueTask.CompletedTask;
        }

        public void SendParcel(string recipient, byte tab, short slot, short qty, int meso)
        {
            var w = new PacketWriter(ClientOps.Get("CP_UserParcelRequest"), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteByte(0x03);          // SEND
            w.WriteByte(tab);
            w.WriteShort(slot);
            w.WriteShort(qty);
            w.WriteInt(meso);
            w.WriteString(recipient);
            w.WriteByte(0);             // trailing flag (as captured)
            Session.SendAsync(w.ToArray()).AsTask().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task Send_TakesGoods_AndReceiveHandsThemOver()
    {
        var repo = new InMemoryCharacterRepository();
        Character sender = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Sender", MapId = 100000000, Meso = 1000 });
        Character receiver = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Recv", MapId = 100000000 });
        sender.EquippedItems.Add(new InventoryItem { ItemId = 4000000, Position = 4, Quantity = 5, CharacterId = sender.Id });
        repo.Save(sender);

        var parcels = new InMemoryParcelRepository();
        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new ParcelClient(sender.Id, (c, _) => c.SendParcel("Recv", tab: 4, slot: 4, qty: 5, meso: 50));
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, parcels: parcels);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        byte result = await client.ParcelResult.Task.WaitAsync(cts.Token);

        Assert.Equal(0x13, result);                                        // 発送しました
        Assert.Equal(950, sender.Meso);                                    // meso deducted
        Assert.DoesNotContain(sender.EquippedItems, i => i.ItemId == 4000000); // whole stack shipped

        ParcelData stored = Assert.Single(parcels.LoadFor(receiver.Id));
        Assert.Equal("Sender", stored.FromName);
        Assert.Equal(50, stored.Meso);
        Assert.Equal(4000000, stored.Item!.ItemId);
        Assert.Equal(5, stored.Item.Quantity);
    }

    [Fact]
    public async Task Send_ToSameAccount_IsRefused()
    {
        var repo = new InMemoryCharacterRepository();
        Character sender = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Sender", MapId = 100000000, Meso = 1000 });
        repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "AltChar", MapId = 100000000 });

        var parcels = new InMemoryParcelRepository();
        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new ParcelClient(sender.Id, (c, _) => c.SendParcel("AltChar", tab: 0, slot: 0, qty: 0, meso: 100));
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, parcels: parcels);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        byte result = await client.ParcelResult.Task.WaitAsync(cts.Token);

        Assert.Equal(0x0F, result);          // 同じID内のキャラクターには送れません
        Assert.Equal(1000, sender.Meso);     // nothing taken
    }
}
