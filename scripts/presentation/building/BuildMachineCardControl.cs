using Godot;

namespace SpaceFactory.Presentation.Building;

public partial class BuildMachineCardControl : PanelContainer
{
    private MachineGlyphControl _glyph = null!;
    private Label _name = null!;
    private Label _category = null!;
    private Label _lockState = null!;
    private Label _description = null!;
    private Label _function = null!;
    private VBoxContainer _costs = null!;
    private Button _select = null!;
    private BuildMachineViewModel? _model;
    private bool _hovered;

    public string MachineId => _model?.MachineId ?? string.Empty;

    public event Action<string>? SelectionRequested;

    public override void _Ready()
    {
        _glyph = GetNode<MachineGlyphControl>("Margin/Layout/Header/Glyph");
        _name = GetNode<Label>("Margin/Layout/Header/Identity/Name");
        _category = GetNode<Label>("Margin/Layout/Header/Identity/Category");
        _lockState = GetNode<Label>("Margin/Layout/Header/LockState");
        _description = GetNode<Label>("Margin/Layout/Description");
        _function = GetNode<Label>("Margin/Layout/Function");
        _costs = GetNode<VBoxContainer>("Margin/Layout/Costs");
        _select = GetNode<Button>("Margin/Layout/Select");
        MouseEntered += HandleMouseEntered;
        MouseExited += HandleMouseExited;
        _select.Pressed += HandleSelectPressed;
        ApplyStyle();
    }

    public override void _ExitTree()
    {
        MouseEntered -= HandleMouseEntered;
        MouseExited -= HandleMouseExited;
        _select.Pressed -= HandleSelectPressed;
    }

    public void Configure(BuildMachineViewModel model, bool compact)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _glyph.Configure(model.Glyph, model.IsUnlocked);
        _name.Text = model.DisplayName.ToUpperInvariant();
        _category.Text = BuildMenuCategoryPresentation.GetDisplayName(model.Category);
        _description.Text = model.Description;
        _function.Text = $"FUNKTION  ·  {model.FunctionSummary}";
        _lockState.Text = model.IsUnlocked ? "VERFÜGBAR" : "GESPERRT";
        _lockState.AddThemeColorOverride(
            "font_color",
            model.IsUnlocked ? BuildingUiTheme.Success : BuildingUiTheme.Warning);
        _select.Disabled = !model.IsUnlocked;
        _select.Text = model.IsUnlocked ? "AUSWÄHLEN" : "NOCH NICHT FREIGESCHALTET";
        TooltipText = model.IsUnlocked
            ? $"{model.DisplayName}\n{model.FunctionSummary}"
            : $"{model.DisplayName}\n{model.UnlockMessage}";
        PopulateCosts(model.Costs);
        SetCompact(compact);
        ApplyStyle();
    }

    public void SetCompact(bool compact)
    {
        CustomMinimumSize = new Vector2(compact ? 310 : 360, compact ? 330 : 350);
        _description.AddThemeFontSizeOverride("font_size", compact ? 12 : 13);
        _function.AddThemeFontSizeOverride("font_size", compact ? 11 : 12);
    }

    private void PopulateCosts(IReadOnlyList<BuildCostViewModel> costs)
    {
        foreach (var child in _costs.GetChildren())
        {
            _costs.RemoveChild(child);
            child.QueueFree();
        }

        if (costs.Count == 0)
        {
            var free = new Label
            {
                Text = "ERSTER BAU KOSTENLOS",
                MouseFilter = MouseFilterEnum.Ignore,
            };
            free.AddThemeColorOverride("font_color", BuildingUiTheme.Success);
            free.AddThemeFontSizeOverride("font_size", 12);
            _costs.AddChild(free);
            return;
        }

        foreach (var cost in costs)
        {
            var row = new HBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore,
            };
            row.AddThemeConstantOverride("separation", 7);
            var marker = new ColorRect
            {
                Color = cost.Accent,
                CustomMinimumSize = new Vector2(7, 18),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            var name = new Label
            {
                Text = cost.DisplayName,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            name.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted.Lightened(0.12f));
            name.AddThemeFontSizeOverride("font_size", 12);
            var amount = new Label
            {
                Text = cost.IsAvailable
                    ? $"{cost.AvailableAmount} / {cost.RequiredAmount}"
                    : $"FEHLT {cost.MissingAmount}  ·  {cost.AvailableAmount} / {cost.RequiredAmount}",
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            amount.AddThemeColorOverride(
                "font_color",
                cost.IsAvailable ? BuildingUiTheme.Success : BuildingUiTheme.Failure);
            amount.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(marker);
            row.AddChild(name);
            row.AddChild(amount);
            _costs.AddChild(row);
        }
    }

    private void HandleMouseEntered()
    {
        _hovered = true;
        ApplyStyle();
    }

    private void HandleMouseExited()
    {
        _hovered = false;
        ApplyStyle();
    }

    private void HandleSelectPressed()
    {
        if (_model is { IsUnlocked: true } model)
        {
            SelectionRequested?.Invoke(model.MachineId);
        }
    }

    private void ApplyStyle()
    {
        var background = _hovered
            ? BuildingUiTheme.CardBackground.Lightened(0.045f)
            : BuildingUiTheme.CardBackground;
        var border = _hovered ? BuildingUiTheme.Accent : BuildingUiTheme.AccentMuted;
        AddThemeStyleboxOverride("panel", BuildingUiTheme.CreatePanelStyle(background, border));
    }
}
