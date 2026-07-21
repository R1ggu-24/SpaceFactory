using Godot;

namespace SpaceFactory.Presentation.Building;

public partial class BuildMenuController : CanvasLayer
{
    private const string MachineCardScenePath = "res://scenes/ui/building/BuildMachineCard.tscn";

    private readonly Dictionary<BuildMenuCategory, Button> _categoryButtons = [];
    private readonly List<BuildMachineCardControl> _cards = [];
    private Control _overlay = null!;
    private MarginContainer _safeArea = null!;
    private PanelContainer _frame = null!;
    private MarginContainer _frameMargin = null!;
    private BoxContainer _body = null!;
    private PanelContainer _categoryPanel = null!;
    private VBoxContainer _categoryButtonsContainer = null!;
    private MarginContainer _catalogViewport = null!;
    private GridContainer _cardGrid = null!;
    private Label _catalogTitle = null!;
    private Label _catalogCount = null!;
    private HBoxContainer _pageBar = null!;
    private Button _previousPage = null!;
    private Button _nextPage = null!;
    private Label _pageLabel = null!;
    private Button _close = null!;
    private IReadOnlyList<BuildMachineViewModel> _machines = [];
    private BuildMenuCategory _selectedCategory = BuildMenuCategory.Processing;
    private Tween? _transitionTween;
    private bool _ready;
    private bool _isOpen;
    private bool _isClosing;
    private string _buildActionLabel = "B";
    private int _pageIndex;
    private int _pageSize = 1;

    public bool IsOpen => _isOpen || _isClosing;

    public BuildMenuCategory SelectedCategory => _selectedCategory;

    public int VisibleMachineCount => _cards.Count;

    public event Action<string>? MachineSelected;

    public event Action? Closed;

    public override void _Ready()
    {
        _overlay = GetNode<Control>("Overlay");
        _safeArea = GetNode<MarginContainer>("Overlay/SafeArea");
        _frame = GetNode<PanelContainer>("Overlay/SafeArea/Frame");
        _frameMargin = GetNode<MarginContainer>("Overlay/SafeArea/Frame/FrameMargin");
        _body = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body");
        _categoryPanel = GetNode<PanelContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CategoryPanel");
        _categoryButtonsContainer = GetNode<VBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CategoryPanel/Margin/Layout/CategoryButtons");
        _catalogViewport = GetNode<MarginContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll");
        _cardGrid = GetNode<GridContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll/Catalog/Cards");
        _catalogTitle = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll/Catalog/Header/Title");
        _catalogCount = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll/Catalog/Header/Count");
        _pageBar = GetNode<HBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll/Catalog/Pages");
        _previousPage = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll/Catalog/Pages/Previous");
        _nextPage = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll/Catalog/Pages/Next");
        _pageLabel = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll/Catalog/Pages/Label");
        _close = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Close");

        _safeArea.Theme = BuildingUiTheme.CreateTheme();
        _frame.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreateFrameStyle());
        _categoryPanel.AddThemeStyleboxOverride(
            "panel",
            BuildingUiTheme.CreatePanelStyle(BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        _close.Pressed += Close;
        _previousPage.Pressed += ShowPreviousPage;
        _nextPage.Pressed += ShowNextPage;
        _frame.Resized += UpdateFramePivot;
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        BuildCategoryButtons();
        _ready = true;
        SetBuildActionLabel(_buildActionLabel);
        RebuildCards();
        UpdateResponsiveLayout();
        UpdateFramePivot();
        Visible = false;
    }

    public override void _ExitTree()
    {
        _close.Pressed -= Close;
        _previousPage.Pressed -= ShowPreviousPage;
        _nextPage.Pressed -= ShowNextPage;
        _frame.Resized -= UpdateFramePivot;
        GetViewport().SizeChanged -= UpdateResponsiveLayout;
        _transitionTween?.Kill();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        if (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void SetMachineCatalog(IReadOnlyList<BuildMachineViewModel> machines)
    {
        ArgumentNullException.ThrowIfNull(machines);
        var duplicate = machines
            .GroupBy(machine => machine.MachineId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate machine id '{duplicate.Key}'.", nameof(machines));
        }

        // Research state is resolved before the catalog reaches this view. Keep
        // the boundary defensive so previews cannot leak a locked entry.
        _machines = machines.Where(machine => machine.IsUnlocked).ToArray();
        EnsureSelectedCategoryHasContent();
        if (_ready)
        {
            RebuildCards();
            RefreshCategoryCounts();
        }
    }

    /// <summary>Updates the visible close hint with the currently configured key.</summary>
    public void SetBuildActionLabel(string actionLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);
        _buildActionLabel = actionLabel.Trim();
    }

    public void Open()
    {
        if (!_ready)
        {
            throw new InvalidOperationException("BuildMenuController must be inside the scene tree before opening.");
        }

        RebuildCards();
        UpdateResponsiveLayout();
        if (_isOpen && !_isClosing)
        {
            return;
        }

        _transitionTween?.Kill();
        _isOpen = true;
        _isClosing = false;
        Visible = true;
        _overlay.Modulate = new Color(1, 1, 1, 0);
        _frame.Scale = new Vector2(0.985f, 0.985f);
        _transitionTween = CreateTween().SetParallel();
        _transitionTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _transitionTween.TweenProperty(_overlay, "modulate", Colors.White, 0.16);
        _transitionTween.TweenProperty(_frame, "scale", Vector2.One, 0.16);
    }

    public void Close()
    {
        if (!_isOpen || _isClosing)
        {
            return;
        }

        _isOpen = false;
        _isClosing = true;
        _transitionTween?.Kill();
        GetViewport().GuiReleaseFocus();
        _transitionTween = CreateTween().SetParallel();
        _transitionTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        _transitionTween.TweenProperty(_overlay, "modulate", new Color(1, 1, 1, 0), 0.12);
        _transitionTween.TweenProperty(_frame, "scale", new Vector2(0.99f, 0.99f), 0.12);
        _transitionTween.SetParallel(false);
        _transitionTween.TweenCallback(Callable.From(FinishClose));
    }

    public void CloseImmediately()
    {
        if (!IsOpen)
        {
            return;
        }

        _transitionTween?.Kill();
        _isOpen = false;
        _isClosing = true;
        FinishClose();
    }

    public void SelectCategory(BuildMenuCategory category)
    {
        if (!_machines.Any(machine => machine.Category == category))
        {
            return;
        }

        if (_selectedCategory == category && _cards.Count > 0)
        {
            return;
        }

        _selectedCategory = category;
        _pageIndex = 0;
        RebuildCards();
    }

    public void RunConstructionSmokeTest()
    {
        if (!_ready)
        {
            throw new InvalidOperationException("Build menu smoke test requires a ready scene.");
        }

        var originalMachines = _machines;
        var originalCategory = _selectedCategory;
        try
        {
            SetMachineCatalog(CreateSmokeCatalog());
            if (_categoryButtons.Count != BuildMenuCategoryPresentation.OrderedCategories.Count)
            {
                throw new InvalidOperationException("Build menu did not construct all six category controls.");
            }

            foreach (var category in BuildMenuCategoryPresentation.OrderedCategories
                         .Where(category => _machines.Any(machine => machine.Category == category)))
            {
                SelectCategory(category);
                var expected = _machines.Count(machine => machine.Category == category);
                if (_cards.Count != expected)
                {
                    throw new InvalidOperationException($"Category {category} rendered {_cards.Count} instead of {expected} machines.");
                }
            }

            if (_machines.Any(machine => !machine.IsUnlocked) ||
                _machines.Any(machine => machine.MachineId == "research") ||
                _categoryButtons[BuildMenuCategory.Research].Visible)
            {
                throw new InvalidOperationException("Locked machines or their empty category remain visible.");
            }

            Vector2[] viewportSizes = [new(800, 600), new(1366, 768), new(1920, 1080), new(2560, 1440)];
            foreach (var size in viewportSizes)
            {
                var metrics = CalculateResponsiveMetrics(size);
                if (metrics.Columns < 1 || metrics.Rows < 1 ||
                    metrics.SafeMargin < 10 || metrics.SafeMargin >= size.X * 0.2f)
                {
                    throw new InvalidOperationException($"Invalid build menu layout at {size.X}x{size.Y}.");
                }
            }

            if (GetNode("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/CatalogScroll") is ScrollContainer)
            {
                throw new InvalidOperationException("Build menu must never expose a catalog scrollbar.");
            }
        }
        finally
        {
            _machines = originalMachines;
            _selectedCategory = originalCategory;
            RebuildCards();
            RefreshCategoryCounts();
        }

        GD.Print("BUILD_MENU_UI_SMOKE_OK: unlocked machines only, empty categories hidden, responsive pagination 800-2560, no scrollbars");
    }

    private void BuildCategoryButtons()
    {
        foreach (var category in BuildMenuCategoryPresentation.OrderedCategories)
        {
            var capturedCategory = category;
            var button = new Button
            {
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 42),
            };
            button.Pressed += () => SelectCategory(capturedCategory);
            _categoryButtonsContainer.AddChild(button);
            _categoryButtons.Add(category, button);
        }

        RefreshCategoryCounts();
    }

    private void RefreshCategoryCounts()
    {
        foreach (var (category, button) in _categoryButtons)
        {
            var count = _machines.Count(machine => machine.Category == category);
            button.Text = $"{BuildMenuCategoryPresentation.GetDisplayName(category)}   {count:00}";
            button.Visible = count > 0;
            button.ButtonPressed = category == _selectedCategory;
        }
    }

    private void RebuildCards()
    {
        if (!_ready)
        {
            return;
        }

        EnsureSelectedCategoryHasContent();

        foreach (var card in _cards)
        {
            card.SelectionRequested -= HandleMachineSelected;
            _cardGrid.RemoveChild(card);
            card.QueueFree();
        }

        _cards.Clear();
        var scene = GD.Load<PackedScene>(MachineCardScenePath) ??
                    throw new InvalidOperationException($"Build machine card scene is missing at {MachineCardScenePath}.");
        var compact = CalculateResponsiveMetrics(GetViewport().GetVisibleRect().Size).Compact;
        foreach (var machine in _machines.Where(machine => machine.Category == _selectedCategory))
        {
            var card = scene.Instantiate<BuildMachineCardControl>();
            _cardGrid.AddChild(card);
            card.Configure(machine, compact);
            card.SelectionRequested += HandleMachineSelected;
            _cards.Add(card);
        }

        var title = BuildMenuCategoryPresentation.GetDisplayName(_selectedCategory);
        _catalogTitle.Text = title;
        _catalogCount.Text = _selectedCategory == BuildMenuCategory.Logistics
            ? $"{_cards.Count:00} VERBINDUNGEN"
            : $"{_cards.Count:00} MASCHINEN";
        RefreshCategoryCounts();
        ApplyPagination();
    }

    private void EnsureSelectedCategoryHasContent()
    {
        if (_machines.Any(machine => machine.Category == _selectedCategory))
        {
            return;
        }

        foreach (var category in BuildMenuCategoryPresentation.OrderedCategories)
        {
            if (_machines.Any(machine => machine.Category == category))
            {
                _selectedCategory = category;
                return;
            }
        }
    }

    private void HandleMachineSelected(string machineId)
    {
        MachineSelected?.Invoke(machineId);
    }

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        var metrics = CalculateResponsiveMetrics(GetViewport().GetVisibleRect().Size);
        SetMargins(_safeArea, metrics.SafeMargin);
        SetMargins(_frameMargin, metrics.FrameMargin);
        _body.Vertical = metrics.Stacked;
        _body.AddThemeConstantOverride("separation", metrics.Stacked ? 12 : 18);
        _categoryPanel.CustomMinimumSize = metrics.Stacked ? new Vector2(0, 238) : new Vector2(metrics.CategoryWidth, 0);
        _categoryPanel.SizeFlagsHorizontal = metrics.Stacked ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;
        _cardGrid.Columns = metrics.Columns;
        _cardGrid.AddThemeConstantOverride("h_separation", metrics.Compact ? 10 : 14);
        _cardGrid.AddThemeConstantOverride("v_separation", metrics.Compact ? 10 : 14);
        _pageSize = Math.Max(1, metrics.Columns * metrics.Rows);
        ApplyPagination();
        foreach (var card in _cards)
        {
            card.SetCompact(metrics.Compact);
        }
    }

    private static ResponsiveMetrics CalculateResponsiveMetrics(Vector2 viewportSize)
    {
        var width = Mathf.Max(640, viewportSize.X);
        var height = Mathf.Max(480, viewportSize.Y);
        var safeMargin = Mathf.RoundToInt(Mathf.Clamp(Mathf.Min(width * 0.035f, height * 0.045f), 14, 54));
        var frameMargin = width < 1000 ? 14 : width < 1600 ? 20 : 26;
        var stacked = width < 940;
        var compact = width < 1500 || height < 850;
        var categoryWidth = compact ? 204 : 228;
        var availableCardWidth = width - (2 * safeMargin) - (2 * frameMargin) - (stacked ? 0 : categoryWidth + 18);
        var columns = availableCardWidth >= 1_080 ? 3 : availableCardWidth >= 690 ? 2 : 1;
        var rows = height >= 1180 ? 3 : height >= 850 ? 2 : 1;
        return new ResponsiveMetrics(stacked, compact, columns, rows, safeMargin, frameMargin, categoryWidth);
    }

    private void ShowPreviousPage()
    {
        _pageIndex = Math.Max(0, _pageIndex - 1);
        ApplyPagination();
    }

    private void ShowNextPage()
    {
        var pageCount = Math.Max(1, (int)Math.Ceiling(_cards.Count / (double)Math.Max(1, _pageSize)));
        _pageIndex = Math.Min(pageCount - 1, _pageIndex + 1);
        ApplyPagination();
    }

    private void ApplyPagination()
    {
        if (!_ready)
        {
            return;
        }

        var safePageSize = Math.Max(1, _pageSize);
        var pageCount = Math.Max(1, (int)Math.Ceiling(_cards.Count / (double)safePageSize));
        _pageIndex = Math.Clamp(_pageIndex, 0, pageCount - 1);
        var first = _pageIndex * safePageSize;
        for (var index = 0; index < _cards.Count; index++)
        {
            _cards[index].Visible = index >= first && index < first + safePageSize;
        }

        _pageBar.Visible = pageCount > 1;
        _pageLabel.Text = $"{_pageIndex + 1:00} / {pageCount:00}";
        _previousPage.Disabled = _pageIndex == 0;
        _nextPage.Disabled = _pageIndex >= pageCount - 1;
    }

    private static void SetMargins(MarginContainer container, int margin)
    {
        container.AddThemeConstantOverride("margin_left", margin);
        container.AddThemeConstantOverride("margin_top", margin);
        container.AddThemeConstantOverride("margin_right", margin);
        container.AddThemeConstantOverride("margin_bottom", margin);
    }

    private void UpdateFramePivot()
    {
        _frame.PivotOffset = _frame.Size * 0.5f;
    }

    private void FinishClose()
    {
        if (!_isClosing)
        {
            return;
        }

        _isClosing = false;
        Visible = false;
        _overlay.Modulate = Colors.White;
        _frame.Scale = Vector2.One;
        Closed?.Invoke();
    }

    private static IReadOnlyList<BuildMachineViewModel> CreateSmokeCatalog()
    {
        var available = new BuildCostViewModel("Eisenplatte", 12, 20, new Color(0.5f, 0.62f, 0.67f));
        var missing = new BuildCostViewModel("Kupferkabel", 8, 3, new Color(0.72f, 0.4f, 0.2f));
        return
        [
            new("crusher", "Zerkleinerer", "Bereitet Rohstoffe für weitere Prozesse auf.", "Zerkleinert Erze und Gestein", BuildMenuCategory.Processing, MachineGlyph.Crusher, [available, missing], true),
            new("smelter", "Schmelzer", "Gewinnt reine Metalle aus Erzen.", "Produziert Metallbarren", BuildMenuCategory.Processing, MachineGlyph.Smelter, [available], true),
            new("constructor", "Konstruktor", "Fertigt grundlegende Bauteile.", "Produziert Platten und Kabel", BuildMenuCategory.Manufacturing, MachineGlyph.Constructor, [available], true),
            new("generator", "Basisgenerator", "Kompakte Startenergie.", "Versorgt erste Maschinen", BuildMenuCategory.Energy, MachineGlyph.BasicGenerator, [], true),
            new("power_cable", "Stromkabel", "Verbindet lokale Stromnetze.", "Verteilt Energie zwischen Maschinen", BuildMenuCategory.Logistics, MachineGlyph.PowerCable, [available], true),
            new("storage", "Lagercontainer", "Lagert Materialien sicher.", "Zusätzlicher Stauraum", BuildMenuCategory.Storage, MachineGlyph.Storage, [available], true),
            new("research", "Forschungsstation", "Erschliesst neue Technologien.", "Schaltet Forschungen frei", BuildMenuCategory.Research, MachineGlyph.Research, [available, missing], false, "Fortgeschrittene Elektronik erforderlich"),
        ];
    }

    private readonly record struct ResponsiveMetrics(
        bool Stacked,
        bool Compact,
        int Columns,
        int Rows,
        int SafeMargin,
        int FrameMargin,
        int CategoryWidth);
}
