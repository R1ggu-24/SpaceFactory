using Godot;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Presentation.UI;

public partial class DebugOverlay : CanvasLayer
{
    private Label _sectorLabel = null!;
    private Label _pauseLabel = null!;

    public override void _Ready()
    {
        _sectorLabel = GetNode<Label>("Margin/Panel/Content/Sector");
        _pauseLabel = GetNode<Label>("Pause");
    }

    public void UpdateSector(long seed, SectorCoordinate coordinate)
    {
        _sectorLabel.Text = $"Welt-Seed: {seed}\nSektor: {coordinate.X}, {coordinate.Y}\nWASD: Fliegen   M: Karte   Esc: Pause";
    }

    public void SetPaused(bool paused) => _pauseLabel.Visible = paused;
}
