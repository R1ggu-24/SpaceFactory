using Godot;
using SpaceFactory.Core.World.Sectors;
using SpaceFactory.Presentation.Settings;

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

    public void RefreshBindings() => RefreshStatus();

    private void RefreshStatus()
    {
        var mode = _isOnFoot ? "Zu Fuß" : "Raumschiff";
        var movement = $"{InputBindingFormatter.FormatAction("move_up")}" +
            $"{InputBindingFormatter.FormatAction("move_left")}" +
            $"{InputBindingFormatter.FormatAction("move_down")}" +
            $"{InputBindingFormatter.FormatAction("move_right")}";
        var controls = _isOnFoot
            ? $"{movement}: Bewegen   {InputBindingFormatter.FormatAction("use_mining_tool")}: Abbauen   " +
              $"{InputBindingFormatter.FormatAction("enter_ship")}: Einsteigen"
            : $"{movement}: Fliegen   {InputBindingFormatter.FormatAction("exit_ship")}: Aussteigen";
        _sectorLabel.Text = $"Welt-Seed: {_seed}\nSektor: {_coordinate.X}, {_coordinate.Y}\nModus: {mode}\n{controls}   " +
            $"{InputBindingFormatter.FormatAction("open_map")}: Karte   {InputBindingFormatter.FormatAction("pause")}: Einstellungen";
    }
}
