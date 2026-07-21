using SpaceFactory.Core.Construction;

namespace SpaceFactory.Core.Tests;

public sealed class PlacementRotationStateTests
{
    [Fact]
    public void NewState_StartsAtZeroAndSharesRotationWithRotatableObjects()
    {
        var state = new PlacementRotationState();

        Assert.Equal(PlacementRotationState.DefaultRotationRadians, state.LastRotationRadians);
        Assert.Equal(0, state.ResolveInitialRotation(supportsRotation: true));

        state.Remember(Math.PI / 4, supportsRotation: true);

        Assert.Equal(Math.PI / 4, state.ResolveInitialRotation(supportsRotation: true), 10);
    }

    [Fact]
    public void NonRotatableObject_UsesZeroWithoutOverwritingRememberedRotation()
    {
        var state = new PlacementRotationState();
        state.Remember(Math.PI / 3, supportsRotation: true);

        var changed = state.Remember(-Math.PI / 2, supportsRotation: false);

        Assert.False(changed);
        Assert.Equal(0, state.ResolveInitialRotation(supportsRotation: false));
        Assert.Equal(Math.PI / 3, state.ResolveInitialRotation(supportsRotation: true), 10);
    }

    [Fact]
    public void Remember_NormalizesAnglesForStableReuse()
    {
        var state = new PlacementRotationState();

        state.Remember((Math.Tau * 2) + (Math.PI / 2), supportsRotation: true);

        Assert.Equal(Math.PI / 2, state.LastRotationRadians, 10);
    }

    [Fact]
    public void Remember_RejectsNonFiniteAngles()
    {
        var state = new PlacementRotationState();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.Remember(double.NaN, supportsRotation: true));
    }
}
