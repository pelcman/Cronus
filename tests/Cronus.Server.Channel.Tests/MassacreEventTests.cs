using Cronus.Common;
using Cronus.Network.Packets;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// The Kerning Subway / Nett's Pyramid massacre port (oracle: Event_PyramidSubway), run against
/// a fake host: what entering a stage sends, how kills and misses move the gauge, the rank and
/// point tables, failure and success, and the free-instance search.
/// </summary>
public class MassacreEventTests
{
    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static readonly ChannelPackets Packets = new(ServerOps, ServerConfig.Jms186);

    private sealed class FakeHost : IMassacreHost
    {
        public int MapId { get; set; } = MassacreEvent.SubwayFirstStage;
        public int PartySize { get; set; }
        public bool IsPartyLeader { get; set; } = true;
        public ChannelPackets Packets => MassacreEventTests.Packets;
        public List<byte[]> Owner { get; } = new();
        public List<byte[]> Party { get; } = new();
        public List<(int Map, int Min, int Max, string? Effect)> PartyWarps { get; } = new();
        public List<int> OwnerWarps { get; } = new();
        public HashSet<int> Busy { get; } = new();
        public List<int> Resets { get; } = new();
        public int ForcedRespawns { get; private set; }
        public int Exp { get; private set; }
        public Dictionary<int, string> Quest { get; } = new();

        public void Clear()
        {
            Owner.Clear();
            Party.Clear();
        }

        public ValueTask SendAsync(byte[] packet) { Owner.Add(packet); return default; }
        public ValueTask SendToPartyInMapAsync(byte[] packet) { Party.Add(packet); return default; }
        public ValueTask WarpPartyAsync(int mapId, int minLevel, int maxLevel, string? screenEffect) { PartyWarps.Add((mapId, minLevel, maxLevel, screenEffect)); MapId = mapId; return default; }
        public ValueTask WarpOwnerAsync(int mapId) { OwnerWarps.Add(mapId); MapId = mapId; return default; }
        public int PlayersOn(int mapId) => Busy.Contains(mapId) ? 1 : 0;
        public void ResetMap(int mapId) => Resets.Add(mapId);
        public void ForceRespawn() => ForcedRespawns++;
        public int CountMobs(int templateId) => 0;
        public ValueTask SpawnMobAtOwnerAsync(int templateId) => default;
        public ValueTask GainExpAsync(int exp) { Exp += exp; return default; }
        public string? GetQuestData(int questId) => Quest.TryGetValue(questId, out string? d) ? d : null;
        public void SetQuestData(int questId, string data) => Quest[questId] = data;
    }

    [Fact]
    public async Task Start_Solo_SendsClockThreeEffectsSixValuesAndTheGauge()
    {
        var host = new FakeHost();
        using var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage);
        await ev.StartAsync();

        // To the party in the map (just the owner here): the 179 s countdown, the three stage
        // effects, then the full gauge.
        Assert.Equal(new[]
        {
            Packets.ClockCountdown(179),
            Packets.FieldEffectScreen("killing/first/number/1"),
            Packets.FieldEffectScreen("killing/first/stage"),
            Packets.FieldEffectScreen("killing/first/start"),
            Packets.MassacreIncGauge(100),
        }, host.Party);

        // To the owner: the six session values the gauge UI reads.
        Assert.Equal(new[]
        {
            Packets.SessionValue("massacre_party", "0"),
            Packets.SessionValue("massacre_miss", "0"),
            Packets.SessionValue("massacre_cool", "0"),
            Packets.SessionValue("massacre_skill", "0"),
            Packets.SessionValue("massacre_laststage", "0"),
            Packets.SessionValue("massacre_hit", "0"),
        }, host.Owner);
    }

    [Fact]
    public async Task Start_AsAPartyMember_SendsNothingUntilTheLeaderDoes()
    {
        var host = new FakeHost { PartySize = 3, IsPartyLeader = false };
        using var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage);
        await ev.StartAsync();
        Assert.Empty(host.Party);
        Assert.Empty(host.Owner);
    }

    [Fact]
    public async Task Kill_RefillsTheGaugeAndReportsTheHitCount()
    {
        var host = new FakeHost();
        using var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage, new Random(7));
        await ev.StartAsync();
        await ev.OnMissAsync();                 // 95
        host.Clear();

        await ev.OnKillAsync();                 // back to 100

        Assert.Equal(1, ev.Kills);
        Assert.Equal(100, ev.Energy);
        Assert.Contains(host.Party, p => p.AsSpan().SequenceEqual(Packets.MassacreIncGauge(100)));
        Assert.Contains(host.Owner, p => p.AsSpan().SequenceEqual(Packets.SessionValue("massacre_hit", "1")));
    }

    [Fact]
    public async Task Miss_DrainsFiveAndReportsTheMissCount()
    {
        var host = new FakeHost();
        using var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage);
        await ev.StartAsync();
        host.Clear();

        await ev.OnMissAsync();

        Assert.Equal(95, ev.Energy);
        Assert.Equal(Packets.MassacreIncGauge(95), host.Party.Single());
        Assert.Equal(Packets.SessionValue("massacre_miss", "1"), host.Owner.Single());
    }

    [Theory]
    [InlineData(-1, 2000, 0)]
    [InlineData(-1, 1999, 1)]
    [InlineData(-1, 1000, 2)]
    [InlineData(-1, 500, 3)]
    [InlineData(-1, 499, 4)]
    [InlineData(0, 3000, 0)]
    [InlineData(2, 2500, 1)]
    [InlineData(3, 1500, 2)]
    [InlineData(1, 499, 4)]
    public void RankFor_FollowsTheOracleThresholds(int type, int kills, int rank)
        => Assert.Equal((byte)rank, MassacreEvent.RankFor(type, kills));

    [Theory]
    [InlineData(-1, 0, 22000)]
    [InlineData(-1, 3, 7000)]
    [InlineData(0, 0, 60500)]
    [InlineData(3, 2, 59500)]
    [InlineData(1, 4, 0)]
    public void PointsFor_FollowsTheOracleTable(int type, int rank, int points)
        => Assert.Equal(points, MassacreEvent.PointsFor(type, (byte)rank));

    [Fact]
    public async Task Fail_WarpsThePartyToTheLobbyWithTheFailBanner_AndEnds()
    {
        var host = new FakeHost();
        var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage);
        await ev.StartAsync();

        await ev.FailAsync();

        Assert.Equal((MassacreEvent.SubwayLobby, 1, 200, "killing/fail"), host.PartyWarps.Single());
        Assert.True(ev.IsDisposed);
    }

    [Fact]
    public async Task ArrivingOnTheResultMap_RecordsKillsAndShowsTheResult()
    {
        var host = new FakeHost();
        host.Quest[MassacreEvent.SubwayQuestRecord] = "40";
        var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage, new Random(1));
        await ev.StartAsync();
        host.Clear();

        await ev.OnChangeMapAsync(MassacreEvent.SubwayResult);

        // No kills: rank 4, no exp, but the record still carries the running total and the
        // client gets the clear banner plus the result window.
        Assert.Equal("40", host.Quest[MassacreEvent.SubwayQuestRecord]);
        Assert.Equal(0, host.Exp);
        Assert.Equal(new[] { Packets.FieldEffectScreen("killing/clear"), Packets.MassacreResult(4, 0) }, host.Owner);
        Assert.True(ev.IsDisposed);
    }

    [Fact]
    public async Task ArrivingOutsideTheRun_EndsItQuietly()
    {
        var host = new FakeHost();
        var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage);
        await ev.StartAsync();
        host.Clear();

        await ev.OnChangeMapAsync(100000000);

        Assert.True(ev.IsDisposed);
        Assert.Empty(host.PartyWarps);
    }

    [Fact]
    public async Task WarpStartSubway_TakesTheFirstFreeInstance_ResetsIt_AndWarpsLevel25To30()
    {
        var host = new FakeHost();
        host.Busy.Add(MassacreEvent.SubwayFirstStage);

        Assert.True(await MassacreEvent.WarpStartSubwayAsync(host));
        Assert.Equal(new[] { MassacreEvent.SubwayFirstStage + 1 }, host.Resets);
        Assert.Equal((MassacreEvent.SubwayFirstStage + 1, 25, 30, (string?)null), host.PartyWarps.Single());

        for (int i = 0; i < MassacreEvent.Instances; i++)
        {
            host.Busy.Add(MassacreEvent.SubwayFirstStage + i);
        }

        Assert.False(await MassacreEvent.WarpStartSubwayAsync(host));
    }

    [Theory]
    [InlineData(910320100, -1)]
    [InlineData(910330200, -1)]
    [InlineData(926010100, 0)]
    [InlineData(926012300, 2)]
    public void TypeOf_SubwayIsMinusOne_PyramidIsItsDifficulty(int mapId, int type)
        => Assert.Equal(type, MassacreEvent.TypeOf(mapId));

    [Fact]
    public async Task ArrivingOnA91033Stage_KeepsTheRunGoing()
    {
        var host = new FakeHost();
        using var ev = new MassacreEvent(host, MassacreEvent.SubwayFirstStage);
        await ev.StartAsync();
        host.Clear();
        host.MapId = 910330200;

        await ev.OnChangeMapAsync(910330200);

        Assert.False(ev.IsDisposed);
        Assert.Equal(Packets.ClockCountdown(179), host.Party[0]);          // stage 2's clock
        Assert.Contains(host.Owner, p => p.AsSpan().SequenceEqual(Packets.SessionValue("massacre_laststage", "1")));
    }

    [Fact]
    public void Packets_CarryTheOracleBytes()
    {
        var r = new PacketReader(Packets.SessionValue("massacre_hit", "12"), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal("massacre_hit", r.ReadString());
        Assert.Equal("12", r.ReadString());
        Assert.Equal(0, r.Remaining);

        r = new PacketReader(Packets.FieldEffectScreen("killing/clear"), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(3, r.ReadByte());                 // FieldEffect_Screen
        Assert.Equal("killing/clear", r.ReadString());
        Assert.Equal(0, r.Remaining);

        r = new PacketReader(Packets.ClockCountdown(179), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(2, r.ReadByte());                 // timer clock
        Assert.Equal(179, r.ReadInt());
        Assert.Equal(0, r.Remaining);

        r = new PacketReader(Packets.MassacreIncGauge(64), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(64, r.ReadInt());
        Assert.Equal(0, r.Remaining);

        r = new PacketReader(Packets.MassacreResult(2, 12345), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(2, r.ReadByte());
        Assert.Equal(12345, r.ReadInt());
        Assert.Equal(0, r.Remaining);
    }
}
