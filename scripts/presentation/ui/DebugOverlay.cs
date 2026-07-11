using Godot;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Presentation.UI;

public partial class DebugOverlay : CanvasLayer
{
    private Label _sectorLabel = null!;
    private Label _pauseLabel = null!;
    private bool _isOnFoot;
    private long _seed;
    private SectorCoordinate _coordinate;

    public override void _Ready()
    {
        _sectorLabel = GetNode<Label>("Margin/Panel/Content/Sector");
        _pauseLabel = GetNode<Label>("Pause");
    }

    public void UpdateSector(long seed, SectorCoordinate coordinate)
    {
        _seed = seed;
        _coordinate = coordinate;
        RefreshStatus();
    }

    public void UpdateControlMode(bool isOnFoot)
    {
        _isOnFoot = isOnFoot;
        RefreshStatus();
    }

    public void SetPaused(bool paused) => _pauseLabel.Visible = paused;

    private void RefreshStatus()
    {
        var mode = _isOnFoot ? "Zu Fuß" : "Raumschiff";
        _sectorLabel.Text = $"Welt-Seed: {_seed}\nSektor: {_coordinate.X}, {_coordinate.Y}\nModus: {mode}\nWASD: Bewegen   F: Modus wechseln   M: Karte   Esc: Pause";
    }
}
