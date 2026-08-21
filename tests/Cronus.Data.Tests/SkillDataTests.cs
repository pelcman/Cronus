using System.Text;
using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class SkillDataTests
{
    // A Skill .img with two skills: 2001005 (3 levels) and 2001004 (name zero-padded, 2 levels).
    private const string SkillXml = """
        <imgdir name="200.img">
          <imgdir name="skill">
            <imgdir name="2001005">
              <imgdir name="level">
                <imgdir name="1"><int name="mpCon" value="5"/></imgdir>
                <imgdir name="2"><int name="mpCon" value="6"/></imgdir>
                <imgdir name="3"><int name="mpCon" value="7"/></imgdir>
              </imgdir>
            </imgdir>
            <imgdir name="02001004">
              <imgdir name="level">
                <imgdir name="1"/>
                <imgdir name="2"/>
              </imgdir>
            </imgdir>
          </imgdir>
        </imgdir>
        """;

    private static WzData Parse(string xml) => WzData.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void SkillImagePath_UsesJobFromSkillId()
    {
        // skillId / 10000, zero-padded to 3 digits, under Skill/.
        Assert.EndsWith(Path.Combine("Skill", "200.img.xml"), WzSkillProvider.SkillImagePath("/wz", 2001005));
        Assert.EndsWith(Path.Combine("Skill", "000.img.xml"), WzSkillProvider.SkillImagePath("/wz", 1000));
    }

    [Fact]
    public void MaxLevelFromWz_CountsLevelEntries()
    {
        Assert.Equal(3, WzSkillProvider.MaxLevelFromWz(Parse(SkillXml), 2001005));
    }

    [Fact]
    public void MaxLevelFromWz_MatchesZeroPaddedSkillNames()
    {
        Assert.Equal(2, WzSkillProvider.MaxLevelFromWz(Parse(SkillXml), 2001004));
    }

    [Fact]
    public void MaxLevelFromWz_UnknownSkill_ReturnsZero()
    {
        Assert.Equal(0, WzSkillProvider.MaxLevelFromWz(Parse(SkillXml), 9999999));
    }

    [Fact]
    public void NullSkillProvider_AlwaysZero()
    {
        Assert.Equal(0, NullSkillProvider.Instance.GetMaxLevel(2001005));
    }
}
