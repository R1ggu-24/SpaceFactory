using SpaceFactory.Core.Common;
using SpaceFactory.Core.Ships.Docking;

namespace SpaceFactory.Core.Tests;

public sealed class ShipDockingTests
{
    [Fact]
    public void DefaultConfiguration_HasDocumentedGameplayValues()
    {
        var configuration = ShipDockingConfiguration.Default;

        Assert.Equal(96, configuration.MaximumAttachmentDistance);
        Assert.Equal(90, configuration.MaximumAttachmentSpeed);
        Assert.Equal(110, configuration.SafeExitDriftSpeed);
        Assert.Equal(0.85, configuration.AstronautVelocityInheritance);
        Assert.Equal(4, configuration.LandingLegAnimationSpeed);
    }

    [Theory]
    [InlineData(96, 90)]
    [InlineData(0, 0)]
    public void Evaluate_ValidCandidateWithinInclusiveLimits_AllowsAttachment(
        double surfaceDistance,
        double shipSpeed)
    {
        var state = new ShipDockingState();
        var context = Context(candidate: Candidate(surfaceDistance: surfaceDistance), shipSpeed: shipSpeed);

        var decision = ShipDockingRules.Evaluate(state, context);

        Assert.Equal(ShipDockingAction.Attach, decision.Action);
        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Evaluate_WithoutCandidate_IsBlocked()
    {
        var decision = ShipDockingRules.Evaluate(new ShipDockingState(), Context(candidate: null));

        Assert.Equal(ShipDockingBlockReason.NoCandidate, decision.BlockReason);
    }

    [Theory]
    [InlineData(96.01, 0, ShipDockingBlockReason.TooFarFromSurface)]
    [InlineData(0, 90.01, ShipDockingBlockReason.MovingTooFast)]
    public void Evaluate_OutsideAttachmentLimits_IsBlocked(
        double surfaceDistance,
        double shipSpeed,
        ShipDockingBlockReason expectedReason)
    {
        var context = Context(Candidate(surfaceDistance: surfaceDistance), shipSpeed);

        var decision = ShipDockingRules.Evaluate(new ShipDockingState(), context);

        Assert.Equal(expectedReason, decision.BlockReason);
        Assert.False(decision.IsAllowed);
    }

    [Theory]
    [InlineData(false, true, ShipDockingBlockReason.SurfaceUnavailable)]
    [InlineData(true, false, ShipDockingBlockReason.UnsafeExitPosition)]
    public void Evaluate_UnsafeSurfaceOrExit_IsBlocked(
        bool hasFreeSurface,
        bool hasSafeExit,
        ShipDockingBlockReason expectedReason)
    {
        var candidate = Candidate(hasFreeSurface: hasFreeSurface, hasSafeExitPosition: hasSafeExit);

        var decision = ShipDockingRules.Evaluate(new ShipDockingState(), Context(candidate));

        Assert.Equal(expectedReason, decision.BlockReason);
    }

    [Theory]
    [InlineData(false, false, ShipDockingBlockReason.PlayerNotControllingShip)]
    [InlineData(true, true, ShipDockingBlockReason.PauseMenuOpen)]
    public void Evaluate_WithoutShipControlOrWhilePaused_IsBlocked(
        bool isShipControlled,
        bool isPauseMenuOpen,
        ShipDockingBlockReason expectedReason)
    {
        var context = Context(
            Candidate(),
            isShipControlled: isShipControlled,
            isPauseMenuOpen: isPauseMenuOpen);

        var decision = ShipDockingRules.Evaluate(new ShipDockingState(), context);

        Assert.Equal(expectedReason, decision.BlockReason);
    }

    [Fact]
    public void TryExecute_Attach_PersistsCometTransformAndStartsLegDeployment()
    {
        var state = new ShipDockingState();
        var candidate = Candidate(
            relativePosition: new WorldPosition(25, -180),
            rotation: 1.25);

        var decision = state.TryExecute(Context(candidate));
        state.AdvanceLandingLegs(0.125);

        Assert.Equal(ShipDockingAction.Attach, decision.Action);
        Assert.True(state.IsAttached);
        Assert.Equal("comet-42", state.AttachedCometId);
        Assert.Equal(new WorldPosition(25, -180), state.RelativeAttachmentPosition);
        Assert.Equal(1.25, state.AttachmentRotationRadians);
        Assert.Equal(0.5, state.LandingLegProgress);
        Assert.False(state.AreLandingLegsDeployed);
    }

    [Fact]
    public void Evaluate_AttachedShip_AllowsDetachWithoutCandidate()
    {
        var state = AttachedState();

        var decision = ShipDockingRules.Evaluate(state, Context(candidate: null, shipSpeed: 500));

        Assert.Equal(ShipDockingAction.Detach, decision.Action);
        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void TryExecute_Detach_ClearsAttachmentAndRetractsLegs()
    {
        var state = AttachedState();
        state.AdvanceLandingLegs(1);

        var decision = state.TryExecute(Context(candidate: null));
        state.AdvanceLandingLegs(0.125);

        Assert.Equal(ShipDockingAction.Detach, decision.Action);
        Assert.False(state.IsAttached);
        Assert.Null(state.AttachedCometId);
        Assert.Equal(default, state.RelativeAttachmentPosition);
        Assert.Equal(0, state.AttachmentRotationRadians);
        Assert.Equal(0.5, state.LandingLegProgress);
    }

    [Fact]
    public void LandingLegAnimation_ClampsAtBothEndpoints()
    {
        var state = AttachedState();

        state.AdvanceLandingLegs(10);
        Assert.Equal(1, state.LandingLegProgress);
        Assert.True(state.AreLandingLegsDeployed);

        state.TryExecute(Context(candidate: null));
        state.AdvanceLandingLegs(10);
        Assert.Equal(0, state.LandingLegProgress);
        Assert.True(state.AreLandingLegsRetracted);
    }

    [Fact]
    public void RestoreAttached_ReconstructsExactPoseAndLandingLegProgress()
    {
        var state = new ShipDockingState();

        state.RestoreAttached("saved-comet", new WorldPosition(12, -34), 1.2, 0.65);

        Assert.True(state.IsAttached);
        Assert.Equal("saved-comet", state.AttachedCometId);
        Assert.Equal(new WorldPosition(12, -34), state.RelativeAttachmentPosition);
        Assert.Equal(1.2, state.AttachmentRotationRadians);
        Assert.Equal(0.65, state.LandingLegProgress);
    }

    [Fact]
    public void RestoreDetached_ClearsAttachmentWithoutLosingAnimationProgress()
    {
        var state = AttachedState();

        state.RestoreDetached(0.3);

        Assert.False(state.IsAttached);
        Assert.Null(state.AttachedCometId);
        Assert.Equal(0.3, state.LandingLegProgress);
    }

    [Fact]
    public void PressGate_HeldKeyProducesOnlyOneAction()
    {
        var gate = new ShipDockingPressGate();

        Assert.True(gate.TryConsume(true));
        gate.Advance(1);
        Assert.False(gate.TryConsume(true));
        Assert.False(gate.TryConsume(true));
    }

    [Fact]
    public void PressGate_RapidSecondPressIsRejectedUntilFreshPressAfterCooldown()
    {
        var gate = new ShipDockingPressGate();

        Assert.True(gate.TryConsume(true));
        Assert.False(gate.TryConsume(false));
        gate.Advance(0.19);
        Assert.False(gate.TryConsume(true));
        gate.Advance(1);
        Assert.False(gate.TryConsume(true));
        Assert.False(gate.TryConsume(false));
        Assert.True(gate.TryConsume(true));
    }

    [Fact]
    public void Drift_FastShipIsClampedWithoutChangingDirection()
    {
        var drift = ShipDriftRules.CalculateUnpilotedDrift(new ShipVelocity(300, 400));

        Assert.Equal(66, drift.X, precision: 8);
        Assert.Equal(88, drift.Y, precision: 8);
        Assert.Equal(110, drift.Speed, precision: 8);
    }

    [Fact]
    public void Drift_SlowShipKeepsItsCurrentVelocity()
    {
        var drift = ShipDriftRules.CalculateUnpilotedDrift(new ShipVelocity(30, -40));

        Assert.Equal(new ShipVelocity(30, -40), drift);
    }

    [Fact]
    public void AstronautExitVelocity_InheritsConfiguredFractionOfSafeShipDrift()
    {
        var velocity = ShipDriftRules.CalculateAstronautExitVelocity(new ShipVelocity(300, 400));

        Assert.Equal(56.1, velocity.X, precision: 8);
        Assert.Equal(74.8, velocity.Y, precision: 8);
        Assert.Equal(93.5, velocity.Speed, precision: 8);
    }

    [Theory]
    [InlineData(0, 90, 110, 0.85, 4)]
    [InlineData(96, -1, 110, 0.85, 4)]
    [InlineData(96, 90, -1, 0.85, 4)]
    [InlineData(96, 90, 110, 1.01, 4)]
    [InlineData(96, 90, 110, 0.85, 0)]
    public void Configuration_InvalidValuesAreRejected(
        double distance,
        double speed,
        double drift,
        double inheritance,
        double legSpeed) => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShipDockingConfiguration(distance, speed, drift, inheritance, legSpeed));

    private static ShipDockingState AttachedState()
    {
        var state = new ShipDockingState();
        state.TryExecute(Context(Candidate()));
        return state;
    }

    private static ShipDockingContext Context(
        ShipDockingCandidate? candidate,
        double shipSpeed = 0,
        bool isShipControlled = true,
        bool isPauseMenuOpen = false) => new(
            isShipControlled,
            isPauseMenuOpen,
            shipSpeed,
            candidate);

    private static ShipDockingCandidate Candidate(
        double surfaceDistance = 20,
        bool hasFreeSurface = true,
        bool hasSafeExitPosition = true,
        WorldPosition relativePosition = default,
        double rotation = 0) => new(
            "comet-42",
            surfaceDistance,
            hasFreeSurface,
            hasSafeExitPosition,
            relativePosition,
            rotation);
}
