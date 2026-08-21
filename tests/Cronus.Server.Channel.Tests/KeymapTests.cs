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

public class KeymapTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void CreateDefault_SeedsReferenceBindings()
    {
        Keymap map = Keymap.CreateDefault();
        Assert.Equal(new KeyBinding(4, 10), map.Get(2));   // first default entry
        Assert.Equal(new KeyBinding(6, 100), map.Get(59)); // an F-key -> menu function
        Assert.Null(map.Get(0));                            // an unbound slot
    }

    [Fact]
    public void FuncKeyMappedInit_Writes94Slots()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.FuncKeyMappedInit(Keymap.CreateDefault()), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(0, r.ReadByte()); // mode 0 = full map

        var slots = new (byte Type, int Action)[Keymap.KeyCount];
        for (int i = 0; i < Keymap.KeyCount; i++)
        {
            slots[i] = (r.ReadByte(), r.ReadInt());
        }

        Assert.Equal(0, r.Remaining);                 // exactly 94 slots
        Assert.Equal(((byte)0, 0), slots[0]);         // unbound
        Assert.Equal(((byte)4, 10), slots[2]);        // default binding at key 2
        Assert.Equal(((byte)6, 100), slots[59]);      // default F-key binding
    }

    /// <summary>Rebinds a key, then sends /pos so the ordered reply proves the rebind was processed.</summary>
    private sealed class Rebinder : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opChat = ServerOps.Get(ServerOpcode.UserChat);
        private bool _sent;

        public Rebinder(int characterId) => _characterId = characterId;

        public TaskCompletionSource Acked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

                // Rebind key 20 to skill 1001003 (Iron Body).
                var rebind = new PacketWriter(ClientOps.Get(ClientOpcode.FuncKeyMappedModified), session.Config.PacketHeaderSize, session.Config.CodePage);
                rebind.WriteInt(0);        // mode 0 = key rebind
                rebind.WriteInt(1);        // one change
                rebind.WriteInt(20);       // key index
                rebind.WriteByte(1);       // type = skill
                rebind.WriteInt(1001003);  // action = skill id
                await session.SendAsync(rebind.ToArray());

                // Then a command whose reply (ordered after the rebind) tells us it was processed.
                var chat = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                chat.WriteInt(0);
                chat.WriteString("/pos");
                chat.WriteByte(0);
                await session.SendAsync(chat.ToArray());
            }
            else if (opcode == _opChat)
            {
                Acked.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task RebindRequest_UpdatesTheKeymap()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Binder", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var keymaps = new KeymapRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Rebinder(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, keymaps: keymaps);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Acked.Task.WaitAsync(cts.Token);

        Assert.Equal(new KeyBinding(1, 1001003), keymaps.Get(hero.Id).Get(20));
    }
}
