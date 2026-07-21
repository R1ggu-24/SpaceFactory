using Godot;
using SpaceFactory.Core.Power;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Procedural top-down machine artwork shared by build cards, the machine panel
/// and placement previews. The silhouettes deliberately mirror the world views,
/// so a machine remains recognisable without relying on its accent colour.
/// </summary>
public partial class MachineGlyphControl : Control
{
    private static readonly Color Titanium = new(0.2f, 0.24f, 0.26f);
    private static readonly Color Graphite = new(0.025f, 0.037f, 0.043f);

    private MachineGlyph _glyph;
    private bool _available = true;
    private Color? _accentOverride;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _ExitTree()
    {
        Resized -= QueueRedraw;
    }

    public void Configure(MachineGlyph glyph, bool available = true, Color? accentOverride = null)
    {
        _glyph = glyph;
        _available = available;
        _accentOverride = accentOverride;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var nominalSize = GetNominalSize(_glyph);
        var availableSize = new Vector2(Mathf.Max(1, Size.X - 8), Mathf.Max(1, Size.Y - 8));
        var scale = Mathf.Min(availableSize.X / nominalSize.X, availableSize.Y / nominalSize.Y);
        var center = Size * 0.5f;
        var accent = _accentOverride ??
                     (_available ? BuildingUiTheme.Accent : BuildingUiTheme.TextMuted.Darkened(0.25f));
        var metal = _available ? Titanium : Titanium.Darkened(0.45f);
        var recess = _available ? Graphite : Graphite.Lightened(0.05f);
        var silhouette = CreateSilhouette(_glyph, nominalSize, center, scale);
        var shadow = silhouette.Select(point => point + new Vector2(2.5f, 3.5f) * scale).ToArray();

        DrawColoredPolygon(shadow, new Color(0, 0, 0, 0.4f));
        DrawColoredPolygon(silhouette, metal.Darkened(0.34f));
        DrawPolyline(Close(silhouette), new Color(accent, 0.8f), Mathf.Max(1, 1.25f * scale), true);
        DrawGlyph(center, scale, accent, metal, recess);
        DrawScrews(center, nominalSize, scale, metal.Lightened(0.28f));
    }

    private void DrawGlyph(Vector2 center, float scale, Color accent, Color metal, Color recess)
    {
        switch (_glyph)
        {
            case MachineGlyph.Crusher:
                DrawCrusher(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Smelter:
                DrawSmelter(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Foundry:
                DrawFoundry(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Constructor:
                DrawConstructor(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Fabricator:
                DrawFabricator(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.WaterProcessor:
                DrawWaterProcessor(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Electrolyzer:
                DrawElectrolyzer(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Refinery:
                DrawRefinery(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.BasicGenerator:
                DrawBasicGenerator(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.FuelGenerator:
                DrawFuelGenerator(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.PowerPole:
                DrawPowerPole(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.PowerCable:
                DrawPowerCable(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.ConveyorBelt:
                DrawConveyorBelt(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.LiquidPipe:
                DrawTransportPipe(center, scale, accent, metal, recess, gas: false);
                break;
            case MachineGlyph.GasPipe:
                DrawTransportPipe(center, scale, accent, metal, recess, gas: true);
                break;
            case MachineGlyph.Storage:
                DrawStorage(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Research:
                DrawResearch(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.MobileMiner:
                DrawMiner(center, scale, accent, metal, recess, automatic: false);
                break;
            case MachineGlyph.AutomaticMiner:
                DrawMiner(center, scale, accent, metal, recess, automatic: true);
                break;
            case MachineGlyph.ChemicalPlant:
                DrawChemicalPlant(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.Assembler:
                DrawAssembler(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.AdvancedFabricator:
                DrawAdvancedFabricator(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.PrecisionManufacturer:
                DrawPrecisionManufacturer(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.LiquidTank:
                DrawFluidTank(center, scale, accent, metal, recess, gas: false);
                break;
            case MachineGlyph.GasTank:
                DrawFluidTank(center, scale, accent, metal, recess, gas: true);
                break;
            case MachineGlyph.PumpStation:
                DrawPumpStation(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.BatteryBank:
                DrawBatteryBank(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.UraniumProcessor:
                DrawUraniumProcessor(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.FuelCellFabricator:
                DrawFuelCellFabricator(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.NuclearReactor:
                DrawNuclearReactor(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.WasteProcessor:
                DrawWasteProcessor(center, scale, accent, metal, recess);
                break;
            case MachineGlyph.NuclearWasteStorage:
                DrawNuclearWasteStorage(center, scale, accent, metal, recess);
                break;
        }
    }

    private void DrawCrusher(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var hopper = ScalePolygon(c, s, [new(-25, -15), new(-8, -11), new(-8, 11), new(-25, 15)]);
        DrawColoredPolygon(hopper, recess);
        DrawPolyline(Close(hopper), metal.Lightened(0.25f), 1.2f * s, true);
        for (var x = -1; x <= 1; x += 2)
        {
            var roller = c + new Vector2(x * 7, 0) * s;
            DrawRect(new Rect2(roller - new Vector2(4, 13) * s, new Vector2(8, 26) * s), metal, true);
            for (var y = -9; y <= 9; y += 6)
            {
                DrawLine(roller + new Vector2(-4, y) * s, roller + new Vector2(4, y + (x * 2)) * s,
                    accent, 1.3f * s, true);
            }
        }
        DrawRect(new Rect2(c + new Vector2(14, -9) * s, new Vector2(13, 18) * s), recess, true);
        DrawWarningStripes(c + new Vector2(14, -13) * s, 13, s, accent);
    }

    private void DrawSmelter(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawCircle(c, 18 * s, metal);
        DrawCircle(c, 13 * s, recess);
        DrawArc(c, 18 * s, 0, Mathf.Tau, 28, metal.Lightened(0.28f), 2 * s, true);
        DrawCircle(c, 7 * s, new Color(accent, 0.72f));
        for (var angle = 0f; angle < Mathf.Tau; angle += Mathf.Pi * 0.5f)
        {
            var direction = Vector2.FromAngle(angle);
            DrawLine(c + direction * 19 * s, c + direction * 27 * s, metal, 4 * s, true);
        }
        DrawVents(c + new Vector2(0, -26) * s, horizontal: true, s, accent.Darkened(0.35f));
    }

    private void DrawFoundry(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        for (var y = -1; y <= 1; y += 2)
        {
            var inlet = c + new Vector2(-21, y * 10) * s;
            DrawCircle(inlet, 7 * s, recess);
            DrawArc(inlet, 7 * s, 0, Mathf.Tau, 16, metal.Lightened(0.2f), 1.5f * s, true);
            DrawLine(inlet + new Vector2(7, 0) * s, c + new Vector2(-3, y * 4) * s, accent, 2 * s, true);
        }
        DrawCircle(c + new Vector2(4, 0) * s, 13 * s, metal);
        DrawCircle(c + new Vector2(4, 0) * s, 8 * s, recess);
        DrawLine(c + new Vector2(16, 0) * s, c + new Vector2(28, 0) * s, accent, 3 * s, true);
        DrawRect(new Rect2(c + new Vector2(23, -9) * s, new Vector2(10, 18) * s), recess, true);
    }

    private void DrawConstructor(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var press = new Rect2(c - new Vector2(10, 12) * s, new Vector2(20, 24) * s);
        DrawRect(press, recess, true);
        DrawRect(press, metal.Lightened(0.18f), false, 1.5f * s, true);
        DrawRect(new Rect2(c + new Vector2(-6, -3) * s, new Vector2(12, 6) * s), accent, true);
        DrawArm(c + new Vector2(-23, -13) * s, c + new Vector2(-9, -5) * s, s, metal, accent);
        DrawArm(c + new Vector2(23, 13) * s, c + new Vector2(9, 5) * s, s, metal, accent);
        DrawPort(c + new Vector2(-29, 0) * s, s, accent, recess);
        DrawPort(c + new Vector2(29, 0) * s, s, accent, recess);
    }

    private void DrawFabricator(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var chamber = ScalePolygon(c, s, [new(-12, -17), new(12, -17), new(18, -10), new(18, 10), new(12, 17), new(-12, 17), new(-18, 10), new(-18, -10)]);
        DrawColoredPolygon(chamber, recess);
        DrawPolyline(Close(chamber), accent, 1.8f * s, true);
        DrawCircle(c, 7 * s, metal);
        DrawCircle(c, 3 * s, accent);
        foreach (var offset in new[] { new Vector2(-31, -13), new Vector2(-31, 0), new Vector2(-31, 13) })
        {
            DrawPort(c + offset * s, s, accent, recess);
            DrawLine(c + (offset + new Vector2(5, 0)) * s, c + new Vector2(-18, offset.Y * 0.48f) * s,
                metal.Lightened(0.15f), 1.5f * s, true);
        }
        DrawRect(new Rect2(c + new Vector2(22, -12) * s, new Vector2(12, 24) * s), new Color(accent, 0.25f), true);
        DrawRect(new Rect2(c + new Vector2(25, -8) * s, new Vector2(6, 3) * s), accent, true);
    }

    private void DrawWaterProcessor(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var hopper = ScalePolygon(c + new Vector2(-20, 0) * s, s, [new(-9, -13), new(8, -9), new(8, 9), new(-9, 13)]);
        DrawColoredPolygon(hopper, metal);
        DrawPolyline(Close(hopper), metal.Lightened(0.25f), 1.2f * s, true);
        DrawTank(c + new Vector2(4, 0) * s, new Vector2(12, 18) * s, accent, recess, s);
        DrawCircle(c + new Vector2(24, 0) * s, 8 * s, recess);
        DrawArc(c + new Vector2(24, 0) * s, 8 * s, 0, Mathf.Tau, 18, accent, 1.6f * s, true);
        DrawLine(c + new Vector2(16, 0) * s, c + new Vector2(20, 0) * s, accent, 2 * s, true);
    }

    private void DrawElectrolyzer(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawTank(c + new Vector2(-9, 0) * s, new Vector2(13, 19) * s, accent, recess, s);
        var hydrogen = new Color(0.22f, 0.84f, 1f);
        var oxygen = new Color(0.72f, 0.9f, 1f);
        DrawTank(c + new Vector2(18, -11) * s, new Vector2(8, 9) * s, hydrogen, recess, s);
        DrawTank(c + new Vector2(18, 11) * s, new Vector2(8, 9) * s, oxygen, recess, s);
        DrawLine(c + new Vector2(4, -5) * s, c + new Vector2(10, -11) * s, hydrogen, 2 * s, true);
        DrawLine(c + new Vector2(4, 5) * s, c + new Vector2(10, 11) * s, oxygen, 2 * s, true);
        DrawCircle(c + new Vector2(-9, 7) * s, 2 * s, new Color(accent, 0.7f));
        DrawCircle(c + new Vector2(-5, 0) * s, 1.5f * s, new Color(accent, 0.55f));
    }

    private void DrawRefinery(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawTank(c + new Vector2(-24, 5) * s, new Vector2(10, 18) * s, accent.Darkened(0.15f), recess, s);
        DrawTank(c + new Vector2(0, -4) * s, new Vector2(12, 23) * s, accent, recess, s);
        DrawTank(c + new Vector2(27, 8) * s, new Vector2(9, 15) * s, accent.Lightened(0.18f), recess, s);
        DrawLine(c + new Vector2(-14, 0) * s, c + new Vector2(-12, 0) * s, metal, 3 * s, true);
        DrawLine(c + new Vector2(12, 3) * s, c + new Vector2(18, 6) * s, metal, 3 * s, true);
        DrawGauge(c + new Vector2(-24, -13) * s, s, accent, recess);
        DrawGauge(c + new Vector2(27, -8) * s, s, accent, recess);
        DrawVents(c + new Vector2(0, 27) * s, true, s, metal.Lightened(0.18f));
    }

    private void DrawBasicGenerator(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawCircle(c, 16 * s, recess);
        DrawArc(c, 16 * s, 0, Mathf.Tau, 24, metal.Lightened(0.22f), 2 * s, true);
        DrawCircle(c, 7 * s, metal);
        for (var i = 0; i < 6; i++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * i / 6f);
            DrawLine(c + direction * 7 * s, c + direction * 14 * s, accent, 2 * s, true);
        }
        DrawPort(c + new Vector2(0, 27) * s, s, accent, recess);
    }

    private void DrawFuelGenerator(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawTank(c + new Vector2(-22, 0) * s, new Vector2(8, 18) * s, accent, recess, s);
        DrawCircle(c + new Vector2(8, 0) * s, 17 * s, recess);
        DrawArc(c + new Vector2(8, 0) * s, 17 * s, 0, Mathf.Tau, 24, metal.Lightened(0.2f), 2 * s, true);
        for (var i = 0; i < 8; i++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * i / 8f);
            DrawLine(c + new Vector2(8, 0) * s + direction * 6 * s,
                c + new Vector2(8, 0) * s + direction * 15 * s, accent, 2 * s, true);
        }
        DrawVents(c + new Vector2(31, 0) * s, false, s, metal.Lightened(0.2f));
    }

    private void DrawPowerPole(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var spine = new Rect2(c + new Vector2(-8, -17) * s, new Vector2(16, 34) * s);
        DrawRect(spine, recess, true);
        DrawRect(spine, metal.Lightened(0.24f), false, 1.5f * s, true);
        DrawRect(new Rect2(c + new Vector2(-3, -14) * s, new Vector2(6, 28) * s),
            new Color(accent, 0.22f), true);
        var portsPerSide = PowerGridConfiguration.PowerPolePortCount / 2;
        for (var row = 0; row < portsPerSide; row++)
        {
            var y = (row - ((portsPerSide - 1) * 0.5f)) * 11f;
            DrawLine(c + new Vector2(-25, y) * s, c + new Vector2(-8, y * 0.72f) * s,
                metal, 3 * s, true);
            DrawLine(c + new Vector2(8, y * 0.72f) * s, c + new Vector2(25, y) * s,
                metal, 3 * s, true);
            DrawPort(c + new Vector2(-25, y) * s, s, accent, recess);
            DrawPort(c + new Vector2(25, y) * s, s, accent, recess);
        }
        DrawRect(new Rect2(c + new Vector2(-6, -3) * s, new Vector2(12, 6) * s), metal, true);
        DrawRect(new Rect2(c + new Vector2(-3, -1) * s, new Vector2(6, 2) * s), accent, true);
    }

    private void DrawPowerCable(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        Vector2[] cable =
        [
            c + new Vector2(-22, 10) * s,
            c + new Vector2(-12, -8) * s,
            c + new Vector2(2, 7) * s,
            c + new Vector2(17, -10) * s,
        ];
        DrawPolyline(cable, recess, 6 * s, true);
        DrawPolyline(cable, accent, 2 * s, true);
        DrawRect(new Rect2(c + new Vector2(-28, 5) * s, new Vector2(8, 10) * s), metal, true);
        DrawRect(new Rect2(c + new Vector2(17, -15) * s, new Vector2(8, 10) * s), metal, true);
        DrawLine(c + new Vector2(-28, 8) * s, c + new Vector2(-32, 8) * s, accent, 1.5f * s, true);
        DrawLine(c + new Vector2(25, -12) * s, c + new Vector2(29, -12) * s, accent, 1.5f * s, true);
    }

    private void DrawConveyorBelt(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var belt = new Rect2(c + new Vector2(-28, -13) * s, new Vector2(56, 26) * s);
        DrawRect(belt, recess, true);
        DrawRect(belt, metal.Lightened(0.16f), false, 2 * s, true);
        for (var x = -22; x <= 22; x += 11)
        {
            DrawCircle(c + new Vector2(x, 0) * s, 5 * s, metal.Darkened(0.12f));
            DrawCircle(c + new Vector2(x, 0) * s, 1.8f * s, accent);
        }
        Vector2[] arrow =
        [
            c + new Vector2(4, -7) * s,
            c + new Vector2(15, 0) * s,
            c + new Vector2(4, 7) * s,
        ];
        DrawPolyline(arrow, accent, 2 * s, true);
    }

    private void DrawTransportPipe(
        Vector2 c,
        float s,
        Color accent,
        Color metal,
        Color recess,
        bool gas)
    {
        Vector2[] pipe =
        [
            c + new Vector2(-28, 8) * s,
            c + new Vector2(-10, 8) * s,
            c + new Vector2(-10, -8) * s,
            c + new Vector2(28, -8) * s,
        ];
        DrawPolyline(pipe, recess, 9 * s, true);
        DrawPolyline(pipe, metal, 6 * s, true);
        DrawPolyline(pipe, new Color(accent, 0.75f), gas ? 1.2f * s : 2.3f * s, true);
        foreach (var point in new[] { pipe[0], pipe[^1] })
        {
            DrawCircle(point, 6 * s, recess);
            DrawArc(point, 5 * s, 0, Mathf.Tau, 14, accent, 1.5f * s, true);
        }
        if (gas)
        {
            DrawCircle(c + new Vector2(3, -8) * s, 2.2f * s, new Color(accent, 0.72f));
            DrawCircle(c + new Vector2(13, -8) * s, 1.3f * s, new Color(accent, 0.55f));
        }
    }

    private void DrawStorage(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        for (var column = -1; column <= 1; column++)
        {
            var door = new Rect2(c + new Vector2((column * 19) - 8, -17) * s, new Vector2(16, 34) * s);
            DrawRect(door, recess, true);
            DrawRect(door, metal.Lightened(0.12f), false, 1.3f * s, true);
            DrawLine(c + new Vector2((column * 19), -12) * s, c + new Vector2(column * 19, 12) * s,
                metal, 1.2f * s, true);
        }
        for (var i = 0; i < 5; i++)
        {
            DrawRect(new Rect2(c + new Vector2(-23 + (i * 5), 21) * s, new Vector2(3, 3) * s),
                i < 3 ? accent : recess, true);
        }
    }

    private void DrawResearch(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawCircle(c, 15 * s, recess);
        DrawCircle(c, 7 * s, new Color(accent, 0.28f));
        DrawArc(c, 20 * s, 0, Mathf.Tau, 28, accent, 1.7f * s, true);
        foreach (var angle in new[] { -Mathf.Pi * 0.5f, Mathf.Pi / 6f, Mathf.Pi * 5f / 6f })
        {
            var direction = Vector2.FromAngle(angle);
            var module = c + direction * 24 * s;
            DrawLine(c + direction * 15 * s, module, metal, 2 * s, true);
            DrawCircle(module, 4 * s, metal);
            DrawCircle(module, 1.8f * s, accent);
        }
        DrawRect(new Rect2(c + new Vector2(21, -15) * s, new Vector2(11, 7) * s), new Color(accent, 0.32f), true);
    }

    private void DrawMiner(Vector2 c, float s, Color accent, Color metal, Color recess, bool automatic)
    {
        var drill = c + new Vector2(automatic ? -7 : 0, 0) * s;
        DrawCircle(drill, (automatic ? 15 : 13) * s, recess);
        DrawArc(drill, (automatic ? 15 : 13) * s, 0, Mathf.Tau, 24, metal.Lightened(0.22f), 2 * s, true);
        for (var index = 0; index < 6; index++)
        {
            var direction = Vector2.FromAngle((Mathf.Tau * index / 6f) + Mathf.Pi / 6f);
            DrawLine(drill + direction * 5 * s, drill + direction * 12 * s, accent, 2 * s, true);
        }
        DrawCircle(drill, 4 * s, accent.Darkened(0.18f));
        foreach (var offset in new[] { new Vector2(-22, -16), new Vector2(-22, 16), new Vector2(18, -16), new Vector2(18, 16) })
        {
            DrawLine(drill + offset * 0.48f * s, c + offset * s, metal, 3 * s, true);
            DrawCircle(c + offset * s, 3.2f * s, accent);
        }
        if (automatic)
        {
            var belt = new Rect2(c + new Vector2(12, -8) * s, new Vector2(25, 16) * s);
            DrawRect(belt, recess, true);
            DrawRect(belt, metal.Lightened(0.18f), false, 1.4f * s, true);
            for (var x = 17; x <= 32; x += 5)
                DrawLine(c + new Vector2(x, -5) * s, c + new Vector2(x, 5) * s, accent, 1.1f * s, true);
        }
        else
        {
            DrawRect(new Rect2(c + new Vector2(-8, 17) * s, new Vector2(16, 8) * s), recess, true);
            DrawRect(new Rect2(c + new Vector2(-5, 19) * s, new Vector2(10, 3) * s), accent, true);
        }
    }

    private void DrawChemicalPlant(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawTank(c + new Vector2(-22, 3) * s, new Vector2(11, 20) * s, accent.Darkened(0.12f), recess, s);
        DrawTank(c + new Vector2(20, -5) * s, new Vector2(13, 22) * s, accent, recess, s);
        DrawCircle(c, 9 * s, recess);
        DrawArc(c, 9 * s, 0, Mathf.Tau, 18, accent.Lightened(0.12f), 1.7f * s, true);
        DrawLine(c + new Vector2(-11, 3) * s, c + new Vector2(-8, 1) * s, metal, 3 * s, true);
        DrawLine(c + new Vector2(8, -1) * s, c + new Vector2(7, -3) * s, metal, 3 * s, true);
        DrawGauge(c + new Vector2(-22, -14) * s, s, accent, recess);
        for (var index = 0; index < 3; index++)
            DrawCircle(c + new Vector2(15 + (index * 4), 12 - (index * 3)) * s,
                (1.2f + (index * 0.35f)) * s, new Color(accent, 0.72f));
    }

    private void DrawAssembler(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var table = new Rect2(c - new Vector2(13, 10) * s, new Vector2(26, 20) * s);
        DrawRect(table, recess, true);
        DrawRect(table, accent, false, 1.5f * s, true);
        DrawArm(c + new Vector2(-27, -17) * s, c + new Vector2(-8, -4) * s, s, metal, accent);
        DrawArm(c + new Vector2(27, 17) * s, c + new Vector2(8, 4) * s, s, metal, accent);
        DrawPort(c + new Vector2(-31, 7) * s, s, accent, recess);
        DrawPort(c + new Vector2(31, -7) * s, s, accent, recess);
        DrawCircle(c, 4 * s, accent);
    }

    private void DrawAdvancedFabricator(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var chamber = ScalePolygon(c, s, [new(-16, -18), new(16, -18), new(22, -10), new(22, 10), new(16, 18), new(-16, 18), new(-22, 10), new(-22, -10)]);
        DrawColoredPolygon(chamber, recess);
        DrawPolyline(Close(chamber), accent, 1.7f * s, true);
        DrawCircle(c, 9 * s, metal);
        DrawArc(c, 6 * s, 0, Mathf.Tau, 18, accent, 2 * s, true);
        foreach (var offset in new[] { new Vector2(-34, -14), new Vector2(-34, 14), new Vector2(34, -14), new Vector2(34, 14) })
        {
            DrawPort(c + offset * s, s, accent, recess);
            DrawLine(c + offset * 0.72f * s, c + offset * s, metal, 2 * s, true);
        }
    }

    private void DrawPrecisionManufacturer(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        var chip = new Rect2(c - new Vector2(14, 14) * s, new Vector2(28, 28) * s);
        DrawRect(chip, recess, true);
        DrawRect(chip, accent, false, 1.6f * s, true);
        for (var index = -2; index <= 2; index++)
        {
            DrawLine(c + new Vector2(-24, index * 5) * s, c + new Vector2(-14, index * 5) * s, metal, 1.8f * s, true);
            DrawLine(c + new Vector2(14, index * 5) * s, c + new Vector2(24, index * 5) * s, metal, 1.8f * s, true);
        }
        DrawCircle(c, 4.5f * s, new Color(accent, 0.82f));
        DrawArm(c + new Vector2(-30, -20) * s, c + new Vector2(-8, -8) * s, s, metal, accent);
        DrawLine(c + new Vector2(0, -25) * s, c + new Vector2(0, -5) * s, accent, 1.4f * s, true);
    }

    private void DrawFluidTank(Vector2 c, float s, Color accent, Color metal, Color recess, bool gas)
    {
        DrawCircle(c, 21 * s, recess);
        DrawArc(c, 21 * s, 0, Mathf.Tau, 30, metal.Lightened(0.22f), 2 * s, true);
        DrawArc(c, 15 * s, 0, Mathf.Tau, 26, new Color(accent, 0.72f), 1.5f * s, true);
        if (gas)
        {
            DrawCircle(c + new Vector2(-6, 3) * s, 3 * s, new Color(accent, 0.45f));
            DrawCircle(c + new Vector2(5, -6) * s, 2.2f * s, new Color(accent, 0.62f));
            DrawCircle(c + new Vector2(7, 7) * s, 1.6f * s, new Color(accent, 0.5f));
        }
        else
            DrawRect(new Rect2(c + new Vector2(-13, 4) * s, new Vector2(26, 10) * s), new Color(accent, 0.32f), true);
        DrawPort(c + new Vector2(0, -27) * s, s, accent, recess);
    }

    private void DrawPumpStation(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawLine(c + new Vector2(-29, 0) * s, c + new Vector2(-14, 0) * s, metal, 7 * s, true);
        DrawLine(c + new Vector2(14, 0) * s, c + new Vector2(29, 0) * s, metal, 7 * s, true);
        DrawCircle(c, 16 * s, recess);
        DrawArc(c, 16 * s, 0, Mathf.Tau, 24, accent, 1.8f * s, true);
        for (var index = 0; index < 5; index++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * index / 5f);
            DrawLine(c + direction * 4 * s, c + direction.Rotated(0.38f) * 12 * s, accent, 2.2f * s, true);
        }
        DrawCircle(c, 3.5f * s, metal.Lightened(0.2f));
    }

    private void DrawBatteryBank(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        for (var column = -2; column <= 2; column++)
        {
            var cell = new Rect2(c + new Vector2((column * 12) - 5, -15) * s, new Vector2(10, 30) * s);
            DrawRect(cell, recess, true);
            DrawRect(cell, metal.Lightened(0.18f), false, 1.2f * s, true);
            DrawRect(new Rect2(c + new Vector2((column * 12) - 3, -11) * s, new Vector2(6, 19) * s),
                new Color(accent, 0.24f + ((column + 2) * 0.1f)), true);
        }
        DrawLine(c + new Vector2(-28, 20) * s, c + new Vector2(28, 20) * s, accent, 2 * s, true);
    }

    private void DrawUraniumProcessor(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawTank(c + new Vector2(-22, 0) * s, new Vector2(12, 22) * s, accent.Darkened(0.18f), recess, s);
        DrawCircle(c + new Vector2(12, 0) * s, 19 * s, recess);
        DrawArc(c + new Vector2(12, 0) * s, 19 * s, 0, Mathf.Tau, 28, metal.Lightened(0.2f), 2.2f * s, true);
        DrawRadiationMark(c + new Vector2(12, 0) * s, 11 * s, accent, recess);
        DrawGauge(c + new Vector2(-22, -17) * s, s, accent, recess);
    }

    private void DrawFuelCellFabricator(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawArm(c + new Vector2(-29, -18) * s, c + new Vector2(-9, -7) * s, s, metal, accent);
        DrawArm(c + new Vector2(29, 18) * s, c + new Vector2(9, 7) * s, s, metal, accent);
        for (var column = -1; column <= 1; column++)
        {
            var cell = new Rect2(c + new Vector2((column * 12) - 4, -17) * s, new Vector2(8, 34) * s);
            DrawRect(cell, recess, true);
            DrawRect(cell, accent, false, 1.3f * s, true);
            DrawRect(new Rect2(cell.Position + new Vector2(2, 5) * s, new Vector2(4, 10) * s), new Color(accent, 0.42f), true);
        }
    }

    private void DrawNuclearReactor(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        DrawCircle(c, 24 * s, recess);
        DrawArc(c, 24 * s, 0, Mathf.Tau, 36, metal.Lightened(0.24f), 2.2f * s, true);
        DrawArc(c, 17 * s, 0, Mathf.Tau, 30, accent, 2.2f * s, true);
        DrawCircle(c, 8 * s, new Color(accent, 0.45f));
        for (var index = 0; index < 8; index++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * index / 8f);
            DrawLine(c + direction * 24 * s, c + direction * 33 * s, metal, 5 * s, true);
            DrawLine(c + direction * 26 * s, c + direction * 31 * s, accent, 1.2f * s, true);
        }
    }

    private void DrawWasteProcessor(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        foreach (var x in new[] { -20f, 20f })
        {
            DrawCircle(c + new Vector2(x, 0) * s, 15 * s, recess);
            DrawArc(c + new Vector2(x, 0) * s, 15 * s, 0, Mathf.Tau, 22, x < 0 ? metal : accent, 1.8f * s, true);
        }
        DrawRadiationMark(c + new Vector2(-20, 0) * s, 8 * s, accent, recess);
        DrawLine(c + new Vector2(-4, 0) * s, c + new Vector2(8, 0) * s, accent, 2.5f * s, true);
        DrawLine(c + new Vector2(3, -5) * s, c + new Vector2(9, 0) * s, accent, 2 * s, true);
        DrawLine(c + new Vector2(3, 5) * s, c + new Vector2(9, 0) * s, accent, 2 * s, true);
    }

    private void DrawNuclearWasteStorage(Vector2 c, float s, Color accent, Color metal, Color recess)
    {
        for (var column = -1; column <= 1; column++)
        {
            var container = new Rect2(c + new Vector2((column * 20) - 8, -18) * s, new Vector2(16, 36) * s);
            DrawRect(container, recess, true);
            DrawRect(container, metal.Lightened(0.16f), false, 1.5f * s, true);
            DrawLine(c + new Vector2((column * 20) - 7, -8) * s,
                c + new Vector2((column * 20) + 7, -8) * s, accent, 2 * s, true);
            DrawRadiationMark(c + new Vector2(column * 20, 5) * s, 6 * s, accent, recess);
        }
    }

    private void DrawRadiationMark(Vector2 center, float radius, Color accent, Color recess)
    {
        DrawCircle(center, radius * 0.2f, accent);
        for (var index = 0; index < 3; index++)
        {
            var direction = Vector2.FromAngle((-Mathf.Pi * 0.5f) + (Mathf.Tau * index / 3f));
            DrawColoredPolygon(
            [
                center + direction * radius * 0.36f,
                center + direction * radius,
                center + direction.Rotated(0.42f) * radius * 0.7f,
            ], accent);
        }
        DrawCircle(center, radius * 0.1f, recess);
    }

    private void DrawTank(Vector2 center, Vector2 radius, Color accent, Color recess, float scale)
    {
        var tank = new Rect2(center - radius, radius * 2);
        DrawRect(tank, recess, true);
        DrawRect(tank, new Color(accent, 0.72f), false, 1.4f * scale, true);
        DrawLine(new Vector2(tank.Position.X, center.Y), new Vector2(tank.End.X, center.Y),
            new Color(accent, 0.35f), scale, true);
    }

    private void DrawArm(Vector2 pivot, Vector2 target, float scale, Color metal, Color accent)
    {
        var elbow = pivot.Lerp(target, 0.55f) + new Vector2(0, (target.X - pivot.X) * 0.15f);
        DrawLine(pivot, elbow, metal.Lightened(0.16f), 3 * scale, true);
        DrawLine(elbow, target, metal.Lightened(0.16f), 3 * scale, true);
        DrawCircle(pivot, 3.5f * scale, accent);
        DrawCircle(elbow, 2.5f * scale, accent.Darkened(0.15f));
    }

    private void DrawPort(Vector2 center, float scale, Color accent, Color recess)
    {
        DrawCircle(center, 4.5f * scale, recess);
        DrawArc(center, 4.5f * scale, 0, Mathf.Tau, 12, accent, 1.2f * scale, true);
    }

    private void DrawGauge(Vector2 center, float scale, Color accent, Color recess)
    {
        DrawCircle(center, 4.5f * scale, recess);
        DrawArc(center, 4.5f * scale, Mathf.Pi, Mathf.Tau, 10, accent, 1.2f * scale, true);
        DrawLine(center, center + new Vector2(2.5f, -2) * scale, accent, scale, true);
    }

    private void DrawVents(Vector2 center, bool horizontal, float scale, Color color)
    {
        for (var index = -2; index <= 2; index++)
        {
            var offset = index * 3.2f;
            var from = horizontal
                ? center + new Vector2(offset, -2) * scale
                : center + new Vector2(-2, offset) * scale;
            var to = horizontal
                ? center + new Vector2(offset, 2) * scale
                : center + new Vector2(2, offset) * scale;
            DrawLine(from, to, color, scale, true);
        }
    }

    private void DrawWarningStripes(Vector2 origin, float width, float scale, Color accent)
    {
        var warning = accent.Lerp(new Color(0.95f, 0.7f, 0.2f), 0.65f);
        for (var x = 1f; x < width; x += 5)
        {
            DrawLine(origin + new Vector2(x, 0) * scale, origin + new Vector2(x + 3, 4) * scale,
                warning, 1.4f * scale, true);
        }
    }

    private void DrawScrews(Vector2 center, Vector2 nominalSize, float scale, Color color)
    {
        var half = (nominalSize * 0.5f) - new Vector2(6, 6);
        foreach (var point in new[]
                 {
                     center + new Vector2(-half.X, -half.Y) * scale,
                     center + new Vector2(half.X, -half.Y) * scale,
                     center + new Vector2(half.X, half.Y) * scale,
                     center + new Vector2(-half.X, half.Y) * scale,
                 })
        {
            DrawCircle(point, Mathf.Max(1, 1.25f * scale), color);
        }
    }

    private static Vector2 GetNominalSize(MachineGlyph glyph) => glyph switch
    {
        MachineGlyph.BasicGenerator or MachineGlyph.PumpStation => new Vector2(48, 48),
        MachineGlyph.PowerPole => new Vector2(56, 48),
        MachineGlyph.Refinery or MachineGlyph.ChemicalPlant or MachineGlyph.UraniumProcessor or
            MachineGlyph.WasteProcessor => new Vector2(70, 52),
        MachineGlyph.Foundry or MachineGlyph.Fabricator or MachineGlyph.Research or MachineGlyph.Assembler or
            MachineGlyph.AdvancedFabricator or MachineGlyph.PrecisionManufacturer or
            MachineGlyph.FuelCellFabricator => new Vector2(66, 52),
        MachineGlyph.Storage or MachineGlyph.BatteryBank or MachineGlyph.NuclearWasteStorage => new Vector2(66, 44),
        MachineGlyph.NuclearReactor => new Vector2(72, 60),
        MachineGlyph.LiquidTank or MachineGlyph.GasTank => new Vector2(56, 56),
        MachineGlyph.PowerCable or MachineGlyph.ConveyorBelt or MachineGlyph.LiquidPipe or MachineGlyph.GasPipe =>
            new Vector2(66, 44),
        MachineGlyph.Crusher or MachineGlyph.Constructor or MachineGlyph.WaterProcessor or
            MachineGlyph.Electrolyzer or MachineGlyph.FuelGenerator or MachineGlyph.MobileMiner or
            MachineGlyph.AutomaticMiner => new Vector2(60, 48),
        _ => new Vector2(52, 52),
    };

    private static Vector2[] CreateSilhouette(
        MachineGlyph glyph,
        Vector2 size,
        Vector2 center,
        float scale)
    {
        var half = size * 0.5f;
        Vector2[] local = glyph switch
        {
            MachineGlyph.Crusher => [new(-half.X, -half.Y + 7), new(-half.X + 7, -half.Y), new(half.X - 10, -half.Y), new(half.X, -half.Y + 10), new(half.X, half.Y - 10), new(half.X - 10, half.Y), new(-half.X + 7, half.Y), new(-half.X, half.Y - 7)],
            MachineGlyph.Smelter => [new(-half.X + 10, -half.Y), new(half.X - 10, -half.Y), new(half.X, -half.Y + 10), new(half.X, half.Y - 10), new(half.X - 10, half.Y), new(-half.X + 10, half.Y), new(-half.X, half.Y - 10), new(-half.X, -half.Y + 10)],
            MachineGlyph.Foundry => [new(-half.X, -half.Y + 7), new(-half.X + 16, -half.Y + 7), new(-half.X + 16, -half.Y), new(half.X - 8, -half.Y), new(half.X, -half.Y + 8), new(half.X, half.Y - 8), new(half.X - 8, half.Y), new(-half.X + 16, half.Y), new(-half.X + 16, half.Y - 7), new(-half.X, half.Y - 7)],
            MachineGlyph.Constructor => [new(-half.X + 7, -half.Y), new(half.X - 13, -half.Y), new(half.X - 13, -half.Y + 5), new(half.X, -half.Y + 5), new(half.X, half.Y - 5), new(half.X - 13, half.Y - 5), new(half.X - 13, half.Y), new(-half.X + 7, half.Y), new(-half.X, half.Y - 7), new(-half.X, -half.Y + 7)],
            MachineGlyph.Fabricator or MachineGlyph.Research or MachineGlyph.AdvancedFabricator or
                MachineGlyph.PrecisionManufacturer or MachineGlyph.FuelCellFabricator or MachineGlyph.NuclearReactor => [new(-half.X + 13, -half.Y), new(half.X - 13, -half.Y), new(half.X, -half.Y + 13), new(half.X, half.Y - 13), new(half.X - 13, half.Y), new(-half.X + 13, half.Y), new(-half.X, half.Y - 13), new(-half.X, -half.Y + 13)],
            MachineGlyph.WaterProcessor or MachineGlyph.Electrolyzer or MachineGlyph.ChemicalPlant or
                MachineGlyph.UraniumProcessor or MachineGlyph.WasteProcessor or MachineGlyph.LiquidTank or
                MachineGlyph.GasTank => [new(-half.X + 9, -half.Y), new(half.X - 9, -half.Y), new(half.X, -half.Y + 9), new(half.X, half.Y - 9), new(half.X - 9, half.Y), new(-half.X + 9, half.Y), new(-half.X, half.Y - 9), new(-half.X, -half.Y + 9)],
            MachineGlyph.Refinery => [new(-half.X, -half.Y + 8), new(-half.X + 12, -half.Y + 8), new(-half.X + 12, -half.Y), new(half.X - 12, -half.Y), new(half.X - 12, -half.Y + 8), new(half.X, -half.Y + 8), new(half.X, half.Y - 8), new(half.X - 12, half.Y - 8), new(half.X - 12, half.Y), new(-half.X + 12, half.Y), new(-half.X + 12, half.Y - 8), new(-half.X, half.Y - 8)],
            MachineGlyph.BasicGenerator or MachineGlyph.MobileMiner or MachineGlyph.AutomaticMiner or
                MachineGlyph.PumpStation => [new(-half.X + 8, -half.Y), new(half.X - 8, -half.Y), new(half.X, -half.Y + 8), new(half.X, half.Y - 8), new(half.X - 8, half.Y), new(-half.X + 8, half.Y), new(-half.X, half.Y - 8), new(-half.X, -half.Y + 8)],
            MachineGlyph.PowerPole => [new(-half.X + 8, -half.Y), new(half.X - 8, -half.Y), new(half.X, -half.Y + 8), new(half.X, half.Y - 8), new(half.X - 8, half.Y), new(-half.X + 8, half.Y), new(-half.X, half.Y - 8), new(-half.X, -half.Y + 8)],
            MachineGlyph.FuelGenerator => [new(-half.X, -half.Y + 6), new(-half.X + 12, -half.Y + 6), new(-half.X + 12, -half.Y), new(half.X - 8, -half.Y), new(half.X, -half.Y + 8), new(half.X, half.Y - 8), new(half.X - 8, half.Y), new(-half.X + 12, half.Y), new(-half.X + 12, half.Y - 6), new(-half.X, half.Y - 6)],
            MachineGlyph.Storage or MachineGlyph.BatteryBank or MachineGlyph.NuclearWasteStorage or
                MachineGlyph.Assembler => [new(-half.X + 6, -half.Y), new(half.X - 6, -half.Y), new(half.X, -half.Y + 6), new(half.X, half.Y - 6), new(half.X - 6, half.Y), new(-half.X + 6, half.Y), new(-half.X, half.Y - 6), new(-half.X, -half.Y + 6)],
            _ => [new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y)],
        };
        return local.Select(point => center + point * scale).ToArray();
    }

    private static Vector2[] ScalePolygon(Vector2 center, float scale, IReadOnlyList<Vector2> points) =>
        points.Select(point => center + point * scale).ToArray();

    private static Vector2[] Close(IReadOnlyList<Vector2> polygon) => [.. polygon, polygon[0]];
}
