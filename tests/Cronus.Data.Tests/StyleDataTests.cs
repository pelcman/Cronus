using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class StyleDataTests
{
    [Fact]
    public void WzStyleProvider_ChecksHairFaceAndSkinAgainstFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "cronus-style-" + Guid.NewGuid().ToString("N"));
        string character = Path.Combine(root, "Character");
        Directory.CreateDirectory(Path.Combine(character, "Hair"));
        Directory.CreateDirectory(Path.Combine(character, "Face"));
        File.WriteAllText(Path.Combine(character, "Hair", "00030030.img.xml"), "<imgdir/>");
        File.WriteAllText(Path.Combine(character, "Face", "00021001.img.xml"), "<imgdir/>");
        File.WriteAllText(Path.Combine(character, "00002000.img.xml"), "<imgdir/>");
        try
        {
            var styles = new WzStyleProvider(root);
            Assert.True(styles.IsValidHair(30030));
            Assert.False(styles.IsValidHair(30031));
            Assert.True(styles.IsValidFace(21001));
            Assert.False(styles.IsValidFace(20000));
            Assert.True(styles.IsValidSkin(0));   // body 2000 exists
            Assert.False(styles.IsValidSkin(3));  // body 2003 does not
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WzStyleProvider_MissingTree_NothingIsValid()
    {
        var styles = new WzStyleProvider(
            Path.Combine(Path.GetTempPath(), "cronus-style-none-" + Guid.NewGuid().ToString("N")));
        Assert.False(styles.IsValidHair(30030));
        Assert.False(styles.IsValidFace(20000));
        Assert.False(styles.IsValidSkin(0));
    }
}
