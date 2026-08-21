using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class UserHitTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class HitClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _damage;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);

        public HitClient(int characterId, int damage)
        {
            _characterId = characterId;
            _damage = damage;
        }

        public TaskCompletionSource<short> HpAfterHit { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = New(session, ClientOpcode.MigrateIn);
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
                var w = New(session, ClientOpcode.UserHit);
                w.WriteInt(0);            // time
                w.WriteByte(0);           // nAttackIdx
                w.WriteByte(0);           // nMagicElemAttr
                w.WriteInt(_damage);      // nDamage
                w.WriteInt(0);            // trailing (mob template) — ignored by the server
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();             // unlock
                int mask = p.ReadInt();
                if ((mask & 0x400) != 0)  // Hp changed
                {
                    HpAfterHit.TrySetResult(p.ReadShort()); // pre-BB HP is 16-bit
                }
            }
        }

        private static PacketWriter New(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    private static async Task<(short Reported, Character Hero)> RunHit(int startHp, int maxHp, int damage)
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Hittee", MapId = 100000000, Hp = (short)startHp, MaxHp = (short)maxHp,
        });

        var client = new HitClient(hero.Id, damage);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        short reported = await client.HpAfterHit.Task.WaitAsync(cts.Token);
        return (reported, hero);
    }

    [Fact]
    public async Task UserHit_AppliesDamage_AndNotifiesHp()
    {
        (short reported, Character hero) = await RunHit(startHp: 100, maxHp: 100, damage: 30);

        Assert.Equal(70, reported);
        Assert.Equal(70, hero.Hp);
    }

    [Fact]
    public async Task UserHit_LethalDamage_FloorsAtOneHp()
    {
        (short reported, Character hero) = await RunHit(startHp: 50, maxHp: 50, damage: 999);

        Assert.Equal(1, reported); // floored at 1 (no death yet)
        Assert.Equal(1, hero.Hp);
    }
}
