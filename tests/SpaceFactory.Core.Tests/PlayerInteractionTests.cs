using SpaceFactory.Core.Common;
using SpaceFactory.Core.Player;

namespace SpaceFactory.Core.Tests;

public sealed class PlayerInteractionTests
{
    [Fact]
    public void CanEnterShip_JustInsideDistance_ReturnsTrue()
    {
        var result = ShipInteractionRules.CanEnterShip(
            true,
            false,
            new WorldPosition(77.9, 0),
            new WorldPosition(0, 0),
            78);

        Assert.True(result);
    }

    [Fact]
    public void CanEnterShip_JustOutsideDistance_ReturnsFalse()
    {
        var result = ShipInteractionRules.CanEnterShip(
            true,
            false,
            new WorldPosition(78.1, 0),
            new WorldPosition(0, 0),
            78);

        Assert.False(result);
    }

    [Fact]
    public void CanEnterShip_WhileMining_ReturnsFalse() => Assert.False(
        ShipInteractionRules.CanEnterShip(true, true, new WorldPosition(0, 0), new WorldPosition(0, 0), 78));

    [Fact]
    public void CanEnterShip_AfterSwitchingControlToShip_ReturnsFalse() => Assert.False(
        ShipInteractionRules.CanEnterShip(false, false, new WorldPosition(0, 0), new WorldPosition(0, 0), 78));

    [Fact]
    public void CanMine_InsideRangeWithHeldInput_ReturnsTrue() => Assert.True(
        MiningInteractionRules.CanMine(
            true,
            false,
            true,
            true,
            new WorldPosition(0, 0),
            new WorldPosition(209.9, 0),
            210));

    [Fact]
    public void CanMine_OutsideRange_ReturnsFalse() => Assert.False(
        MiningInteractionRules.CanMine(
            true,
            false,
            true,
            true,
            new WorldPosition(0, 0),
            new WorldPosition(210.1, 0),
            210));

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CanMine_WithBlockedInputOrEmptyTarget_ReturnsFalse(
        bool miningInputHeld,
        bool targetHasMaterial,
        bool isUiBlocked) => Assert.False(
        MiningInteractionRules.CanMine(
            true,
            isUiBlocked,
            miningInputHeld,
            targetHasMaterial,
            new WorldPosition(0, 0),
            new WorldPosition(10, 0),
            210));

    [Fact]
    public void MiningSession_Cancel_ResetsProgress()
    {
        var session = new MiningSession();
        session.Begin("deposit", 4);
        session.Advance(2);

        session.Cancel();

        Assert.False(session.IsActive);
        Assert.Equal(0, session.Progress);
    }

    [Fact]
    public void MiningSession_ChangingTarget_ResetsProgress()
    {
        var session = new MiningSession();
        session.Begin("first", 4);
        session.Advance(2);

        session.Begin("second", 4);

        Assert.Equal("second", session.TargetId);
        Assert.Equal(0, session.Progress);
    }

    [Fact]
    public void MiningSession_RequiresFullDuration()
    {
        var session = new MiningSession();
        session.Begin("deposit", 3);

        Assert.False(session.Advance(2.9));
        Assert.True(session.Advance(0.1));
        Assert.Equal(1, session.Progress);
    }
}
