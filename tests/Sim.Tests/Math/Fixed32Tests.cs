using Rts.Sim.Math;

namespace Rts.Sim.Tests.MathTests;

public class Fixed32Tests
{
    [Fact]
    public void Constants()
    {
        Assert.Equal(0, Fixed32.Zero.Raw);
        Assert.Equal(65536, Fixed32.One.Raw);
        Assert.Equal(32768, Fixed32.Half.Raw);
    }

    [Fact]
    public void FromIntAndToIntFloor()
    {
        Assert.Equal(3, Fixed32.FromInt(3).ToInt());
        Assert.Equal(-3, Fixed32.FromInt(-3).ToInt());
        Assert.Equal(-1, Fixed32.FromRaw(-1).ToInt());
        Assert.Equal(-2, Fixed32.FromRatio(-3, 2).ToInt());
        Assert.Equal(1, Fixed32.FromRatio(3, 2).ToInt());
    }

    [Theory]
    [InlineData(1, 3, 21845)]
    [InlineData(2, 3, 43691)]
    [InlineData(-1, 3, -21845)]
    [InlineData(-2, 3, -43691)]
    [InlineData(1, -3, -21845)]
    [InlineData(-1, -3, 21845)]
    [InlineData(1, 2, 32768)]
    [InlineData(3, 2, 98304)]
    public void FromRatioRounds(int n, int d, int raw) => Assert.Equal(raw, Fixed32.FromRatio(n, d).Raw);

    [Fact]
    public void FromRatioTiesAwayFromZero()
    {
        Assert.Equal(1, Fixed32.FromRatio(1, 131072).Raw);
        Assert.Equal(-1, Fixed32.FromRatio(-1, 131072).Raw);
    }

    [Fact]
    public void AddSubNeg()
    {
        var a = Fixed32.FromRatio(3, 2);
        var b = Fixed32.FromRatio(-1, 4);
        Assert.Equal(Fixed32.FromRatio(5, 4), a + b);
        Assert.Equal(Fixed32.FromRatio(7, 4), a - b);
        Assert.Equal(Fixed32.FromRatio(-3, 2), -a);
    }

    [Fact]
    public void AddWraps()
    {
        Assert.Equal(int.MinValue, (Fixed32.FromRaw(int.MaxValue) + Fixed32.FromRaw(1)).Raw);
    }

    [Fact]
    public void MulRoundsTowardZero()
    {
        Assert.Equal(Fixed32.FromRatio(3, 4), Fixed32.Half * Fixed32.FromRatio(3, 2));
        Assert.Equal(Fixed32.FromRatio(-3, 4), Fixed32.Half * Fixed32.FromRatio(-3, 2));
        Assert.Equal(0, (Fixed32.FromRaw(1) * Fixed32.Half).Raw);
        Assert.Equal(0, (Fixed32.FromRaw(-1) * Fixed32.Half).Raw);
        Assert.Equal(-1, (Fixed32.FromRaw(-3) * Fixed32.Half).Raw);
    }

    [Fact]
    public void DivRoundsTowardZero()
    {
        Assert.Equal(Fixed32.FromInt(3), Fixed32.FromRatio(3, 2) / Fixed32.Half);
        Assert.Equal(21845, (Fixed32.One / Fixed32.FromInt(3)).Raw);
        Assert.Equal(-21845, (Fixed32.FromInt(-1) / Fixed32.FromInt(3)).Raw);
        Assert.Equal(-21845, (Fixed32.One / Fixed32.FromInt(-3)).Raw);
        Assert.Throws<DivideByZeroException>(() => Fixed32.One / Fixed32.Zero);
    }

    [Fact]
    public void Round()
    {
        Assert.Equal(2, Fixed32.FromRatio(3, 2).Round());
        Assert.Equal(-2, Fixed32.FromRatio(-3, 2).Round());
        Assert.Equal(1, Fixed32.FromRatio(5, 4).Round());
        Assert.Equal(-1, Fixed32.FromRatio(-5, 4).Round());
        Assert.Equal(0, Fixed32.FromRatio(1, 4).Round());
    }

    [Fact]
    public void AbsMinMax()
    {
        var n = Fixed32.FromInt(-2);
        var p = Fixed32.FromInt(5);
        Assert.Equal(Fixed32.FromInt(2), n.Abs());
        Assert.Equal(n, Fixed32.Min(n, p));
        Assert.Equal(p, Fixed32.Max(n, p));
    }

    [Fact]
    public void MulInt()
    {
        Assert.Equal(7, Fixed32.FromRatio(7, 2).MulInt(2));
        Assert.Equal(1, Fixed32.FromRatio(3, 2).MulInt(1));
        Assert.Equal(-1, Fixed32.FromRatio(-3, 2).MulInt(1));
    }

    [Fact]
    public void Comparisons()
    {
        var a = Fixed32.FromInt(1);
        var b = Fixed32.FromInt(2);
        Assert.True(a < b);
        Assert.True(b > a);
        Assert.True(a <= Fixed32.One);
        Assert.True(a >= Fixed32.One);
        Assert.True(a != b);
        Assert.True(a == Fixed32.One);
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(a.Equals(Fixed32.One));
    }

    [Theory]
    [InlineData(65536 + 32768, "1.5000")]
    [InlineData(0, "0.0000")]
    [InlineData(-32768, "-0.5000")]
    [InlineData(1, "0.0000")]
    [InlineData(65535, "1.0000")]
    [InlineData(21845, "0.3333")]
    [InlineData(int.MinValue, "-32768.0000")]
    public void ToStringFormat(int raw, string expected) => Assert.Equal(expected, Fixed32.FromRaw(raw).ToString());
}
