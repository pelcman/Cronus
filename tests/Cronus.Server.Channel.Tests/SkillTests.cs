using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class SkillTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class SkillClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _skillId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opSkillResult = ServerOps.Get(ServerOpcode.ChangeSkillRecordResult);

        public SkillClient(int characterId, int skillId)
        {
            _characterId = characterId;
            _skillId = skillId;
        }

        public TaskCompletionSource<(int SkillId, int Level)> SkillLearned { get; } =
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
                var w = New(session, ClientOpcode.UserSkillUpRequest);
                w.WriteInt(0);           // timestamp
                w.WriteInt(_skillId);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opSkillResult)
            {
                p.ReadByte();            // 1
                p.ReadShort();           // count
                int skillId = p.ReadInt();
                int level = p.ReadInt();
                SkillLearned.TrySetResult((skillId, level));
            }
        }

        private static PacketWriter New(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    [Fact]
    public async Task SkillUp_SpendsSp_AndConfirmsSkill()
    {
        const int skillId = 1000001; // a warrior skill
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Adept", MapId = 100000000, Job = 100, Sp = 3,
        });

        var client = new SkillClient(hero.Id, skillId);
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

        (int learnedSkill, int level) = await client.SkillLearned.Task.WaitAsync(cts.Token);

        Assert.Equal(skillId, learnedSkill);
        Assert.Equal(1, level);
        Assert.Equal(1, hero.Skills[skillId]);
        Assert.Equal(2, hero.Sp); // 3 -> 2
    }

    [Fact]
    public async Task SkillUp_WithNoSp_DoesNothing()
    {
        const int skillId = 1000001;
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Broke", MapId = 100000000, Job = 100, Sp = 0,
        });

        var client = new SkillClient(hero.Id, skillId);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SkillLearned.Task.WaitAsync(cts.Token));
        Assert.False(hero.Skills.ContainsKey(skillId));
        Assert.Equal(0, hero.Sp);
    }

    private sealed class StubSkillProvider(int max) : Cronus.Data.ISkillProvider
    {
        public int GetMaxLevel(int skillId) => max;

        public Cronus.Data.SkillEffect? GetSkillEffect(int skillId, int level) => null;

        public Cronus.Data.MobSkillData? GetMobSkill(int skillId, int level) => null;
    }

    [Fact]
    public async Task SkillUp_AtMaxLevel_DoesNothing()
    {
        const int skillId = 1000001;
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Maxed", MapId = 100000000, Job = 100, Sp = 3,
        });
        hero.Skills[skillId] = 1; // already at the (stubbed) max level of 1

        var client = new SkillClient(hero.Id, skillId);
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, skills: new StubSkillProvider(1));

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SkillLearned.Task.WaitAsync(cts.Token));
        Assert.Equal(1, hero.Skills[skillId]); // unchanged
        Assert.Equal(3, hero.Sp);              // no SP spent
    }
}
