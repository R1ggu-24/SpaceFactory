using SpaceFactory.Core.Common;
using SpaceFactory.Core.Player;

namespace SpaceFactory.Core.Tests;

public sealed class PlayerInteractionTests
{
    [Fact]
    public void AvailableShipAction_InShip_IsExit()
    {
        var action = ShipInteractionRules.GetAvailableAction(
            PlayerControlMode.Ship,
            false,
            false,
            new WorldPosition(1000, 1000),
            new WorldPosition(0, 0),
            78);

        Assert.Equal(ShipInteractionAction.ExitShip, action);
    }

    [Fact]
    public void AvailableShipAction_OnFootInsideRange_IsEnter()
    {
        var action = ShipInteractionRules.GetAvailableAction(
            PlayerControlMode.OnFoot,
            false,
            false,
            new WorldPosition(77.9, 0),
            new WorldPosition(0, 0),
            78);

        Assert.Equal(ShipInteractionAction.EnterShip, action);
    }

    [Fact]
    public void AvailableShipAction_OnFootOutsideRange_IsNone()
    {
        var action = ShipInteractionRules.GetAvailableAction(
            PlayerControlMode.OnFoot,
            false,
            false,
            new WorldPosition(78.1, 0),
            new WorldPosition(0, 0),
            78);

        Assert.Equal(ShipInteractionAction.None, action);
    }

    [Fact]
    public void AvailableShipAction_InShipWhileUiBlocked_IsNone()
    {
        var action = ShipInteractionRules.GetAvailableAction(
            PlayerControlMode.Ship,
            false,
            true,
            new WorldPosition(0, 0),
            new WorldPosition(0, 0),
            78);

        Assert.Equal(ShipInteractionAction.None, action);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AvailableShipAction_OnFootWhileMiningOrBlocked_IsNone(bool isMining, bool isBlocked)
    {
        var action = ShipInteractionRules.GetAvailableAction(
            PlayerControlMode.OnFoot,
            isMining,
            isBlocked,
            new WorldPosition(0, 0),
            new WorldPosition(0, 0),
            78);

        Assert.Equal(ShipInteractionAction.None, action);
    }

    [Fact]
    public void ShipInteractionPressGate_HeldPressIsAcceptedOnlyOnce()
    {
        var gate = new ShipInteractionPressGate();

        Assert.True(gate.TryPress());
        Assert.False(gate.TryPress());
        gate.Advance(1);
        Assert.False(gate.TryPress());
    }

    [Fact]
    public void ShipInteractionPressGate_RapidSecondPressIsRejected()
    {
        var gate = new ShipInteractionPressGate(0.25);

        Assert.True(gate.TryPress());
        gate.Release();
        gate.Advance(0.24);

        Assert.False(gate.TryPress());
    }

    [Fact]
    public void ShipInteractionPressGate_NewPressAfterCooldownIsAccepted()
    {
        var gate = new ShipInteractionPressGate(0.25);

        Assert.True(gate.TryPress());
        gate.Release();
        gate.Advance(0.25);

        Assert.True(gate.TryPress());
    }

    [Fact]
    public void ShipInteractionPressGate_ResetAfterUiChangeAllowsFreshPress()
    {
        var gate = new ShipInteractionPressGate();
        Assert.True(gate.TryPress());

        gate.Reset();

        Assert.False(gate.IsHeld);
        Assert.Equal(0, gate.RemainingCooldownSeconds);
        Assert.True(gate.TryPress());
    }

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
