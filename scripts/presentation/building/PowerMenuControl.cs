using Godot;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Compact non-pausing power inspector. The full-screen transparent root
/// consumes pointer input so clicks never reach mining, placement or the world.
/// </summary>
public partial class PowerMenuControl : Control
{
    private readonly Dictionary<string, MetricWidgets> _metrics = [];
    private readonly Dictionary<string, PortWidgets> _portWidgets = [];
    private readonly Dictionary<string, SourceWidgets> _sourceWidgets = [];
    private PanelContainer _frame = null!;
    private GridContainer _metricGrid = null!;
    private Label _title = null!;
    private Label _networkName = null!;
    private Label _status = null!;
    private Button _networkToggle = null!;
    private PowerHistoryChartControl _history = null!;
    private VBoxContainer _sources = null!;
    private Label _sourcesEmpty = null!;
    private VBoxContainer _ports = null!;
    private Label _portsEmpty = null!;
    private PowerMenuViewModel? _model;
    private bool _ready;
    private bool _requestedOpen;
    private bool _suppressEvents;

    public bool IsOpen => _requestedOpen;

    public event Action<bool>? NetworkEnabledChangeRequested;

    public event Action<string, bool>? PortEnabledChangeRequested;

    public event Action<string, bool>? SourceEnabledChangeRequested;

    public event Action<string>? DisconnectPortRequested;

    public event Action? Closed;

    public override void _Ready()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "PowerMenu" : Name;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;
        BuildInterface();
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _ready = true;
        UpdateResponsiveLayout();
        if (_model is not null)
        {
            Render(_model);
        }

        Visible = _requestedOpen;
    }

    public override void _ExitTree()
    {
        if (_ready)
        {
            GetViewport().SizeChanged -= UpdateResponsiveLayout;
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_requestedOpen && @event is InputEventMouse)
        {
            AcceptEvent();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_requestedOpen || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(PowerMenuViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _requestedOpen = true;
        _model = model;
        if (!_ready)
        {
            return;
        }

        Render(model);
        Visible = true;
        MoveToFront();
    }

    public void UpdateView(PowerMenuViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        if (_ready)
        {
            Render(model);
        }
    }

    public void Close()
    {
        if (!_requestedOpen)
        {
            return;
        }

        _requestedOpen = false;
        Visible = false;
        Closed?.Invoke();
    }

    private void BuildInterface()
    {
        Theme = BuildingUiTheme.CreateTheme();

        var dimmer = new ColorRect
        {
            Name = "WorldInputBlocker",
            Color = new Color(0, 0.008f, 0.013f, 0.18f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        dimmer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dimmer);

        var center = new CenterContainer
        {
            Name = "Center",
            MouseFilter = MouseFilterEnum.Pass,
        };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _frame = new PanelContainer
        {
            Name = "Frame",
            MouseFilter = MouseFilterEnum.Stop,
        };
        _frame.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreateFrameStyle());
        center.AddChild(_frame);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        _frame.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 10);
        margin.AddChild(layout);

        layout.AddChild(CreateHeader());
        layout.AddChild(CreateSeparator());

        var bodyScroll = new ScrollContainer
        {
            Name = "BodyScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        layout.AddChild(bodyScroll);

        var body = new VBoxContainer
        {
            Name = "Body",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 10);
        bodyScroll.AddChild(body);

        _metricGrid = new GridContainer
        {
            Name = "Metrics",
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _metricGrid.AddThemeConstantOverride("h_separation", 7);
        _metricGrid.AddThemeConstantOverride("v_separation", 7);
        body.AddChild(_metricGrid);
        AddMetric("capacity", "KAPAZITÄT");
        AddMetric("production", "PRODUKTION");
        AddMetric("consumption", "VERBRAUCH");
        AddMetric("demand", "BEDARF");
        AddMetric("reserve", "RESERVE");
        AddMetric("fuel", "TREIBSTOFF / MIN");
        AddMetric("runtime", "RESTLAUFZEIT");
        AddMetric("status", "NETZZUSTAND");

        body.AddChild(CreateHistoryPanel());
        body.AddChild(CreateSourcesPanel());
        body.AddChild(CreatePortsPanel());
    }

    private Control CreateHeader()
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        var identity = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        identity.AddThemeConstantOverride("separation", 1);
        _title = new Label { Text = "STROMNETZ" };
        _title.AddThemeFontSizeOverride("font_size", 22);
        _title.AddThemeColorOverride("font_color", Colors.White);
        identity.AddChild(_title);
        _networkName = new Label { Text = "KEIN NETZ" };
        _networkName.AddThemeFontSizeOverride("font_size", 11);
        _networkName.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        identity.AddChild(_networkName);
        header.AddChild(identity);

        _status = new Label
        {
            Text = "AUS",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(104, 34),
        };
        _status.AddThemeStyleboxOverride("normal", BuildingUiTheme.CreatePanelStyle(
            BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        header.AddChild(_status);

        _networkToggle = new Button
        {
            ToggleMode = true,
            Text = "NETZ AUS",
            CustomMinimumSize = new Vector2(108, 36),
        };
        _networkToggle.Toggled += HandleNetworkToggled;
        header.AddChild(_networkToggle);

        var close = new Button
        {
            Text = "×",
            TooltipText = "Strommenü schließen",
            CustomMinimumSize = new Vector2(38, 36),
        };
        close.AddThemeFontSizeOverride("font_size", 22);
        close.Pressed += Close;
        header.AddChild(close);
        return header;
    }

    private Control CreateHistoryPanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreatePanelStyle(
            BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        var margin = CreatePanelMargin();
        panel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 5);
        margin.AddChild(layout);
        var heading = new Label { Text = "LEISTUNG · LETZTE 60 SEKUNDEN" };
        heading.AddThemeFontSizeOverride("font_size", 12);
        heading.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        layout.AddChild(heading);
        _history = new PowerHistoryChartControl
        {
            CustomMinimumSize = new Vector2(200, 138),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        layout.AddChild(_history);
        return panel;
    }

    private Control CreatePortsPanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreatePanelStyle(
            BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        var margin = CreatePanelMargin();
        panel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 6);
        margin.AddChild(layout);
        var heading = new Label { Text = "ANSCHLÜSSE" };
        heading.AddThemeFontSizeOverride("font_size", 12);
        heading.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        layout.AddChild(heading);
        _ports = new VBoxContainer
        {
            Name = "PortRows",
        };
        _ports.AddThemeConstantOverride("separation", 4);
        layout.AddChild(_ports);
        _portsEmpty = new Label
        {
            Text = "Keine Stromanschlüsse verfügbar",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _portsEmpty.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        _ports.AddChild(_portsEmpty);
        return panel;
    }

    private Control CreateSourcesPanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreatePanelStyle(
            BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        var margin = CreatePanelMargin();
        panel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 6);
        margin.AddChild(layout);
        var heading = new Label { Text = "STROMQUELLEN" };
        heading.AddThemeFontSizeOverride("font_size", 12);
        heading.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        layout.AddChild(heading);
        _sources = new VBoxContainer
        {
            Name = "SourceRows",
        };
        _sources.AddThemeConstantOverride("separation", 4);
        layout.AddChild(_sources);
        _sourcesEmpty = new Label
        {
            Text = "Keine aktive Stromquelle in diesem Netz",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _sourcesEmpty.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        _sources.AddChild(_sourcesEmpty);
        return panel;
    }

    private void AddMetric(string key, string caption)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(128, 54),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        card.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreatePanelStyle(
            new Color(0.006f, 0.035f, 0.048f, 0.96f),
            new Color(0.035f, 0.3f, 0.39f, 0.76f)));
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 1);
        var title = new Label { Text = caption };
        title.AddThemeFontSizeOverride("font_size", 9);
        title.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        var value = new Label { Text = "—" };
        value.AddThemeFontSizeOverride("font_size", 15);
        value.AddThemeColorOverride("font_color", BuildingUiTheme.Text);
        layout.AddChild(title);
        layout.AddChild(value);
        card.AddChild(layout);
        _metricGrid.AddChild(card);
        _metrics.Add(key, new MetricWidgets(value));
    }

    private void Render(PowerMenuViewModel model)
    {
        _suppressEvents = true;
        try
        {
            _title.Text = model.Title.ToUpperInvariant();
            _networkName.Text = string.IsNullOrWhiteSpace(model.NetworkDisplayName)
                ? "KEIN VERBUNDENES NETZ"
                : model.NetworkDisplayName.ToUpperInvariant();
            _networkToggle.ButtonPressed = model.IsNetworkEnabled;
            _networkToggle.Disabled = !model.CanToggleNetwork;
            _networkToggle.Text = model.IsNetworkEnabled ? "NETZ AN" : "NETZ AUS";
            var statusText = GetStatusText(model.Status);
            var statusColor = GetStatusColor(model.Status);
            _status.Text = statusText;
            _status.AddThemeColorOverride("font_color", statusColor);

            SetMetric("capacity", FormatPower(model.Capacity));
            SetMetric("production", FormatPower(model.ActualProduction));
            SetMetric("consumption", FormatPower(model.ActualConsumption));
            SetMetric("demand", FormatPower(model.RequestedPower));
            SetMetric("reserve", FormatSignedPower(model.Reserve), model.Reserve < 0 ? BuildingUiTheme.Failure : BuildingUiTheme.Success);
            SetMetric("fuel", $"{Math.Max(0, model.FuelConsumptionPerMinute):0.00} E");
            SetMetric("runtime", FormatRuntime(model.EstimatedFuelMinutesRemaining));
            SetMetric("status", statusText, statusColor);
            _history.SetSamples(model.History ?? []);
            RefreshSourceRows(model.Sources ?? []);
            RefreshPortRows(model.Ports ?? []);
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void RefreshPortRows(IReadOnlyList<PowerPortViewModel> ports)
    {
        var incomingIds = ports.Select(port => port.PortId).ToArray();
        var existingIds = _portWidgets.Keys.ToArray();
        if (!incomingIds.SequenceEqual(existingIds, StringComparer.Ordinal))
        {
            foreach (var widgets in _portWidgets.Values)
            {
                widgets.Root.QueueFree();
            }

            _portWidgets.Clear();
            foreach (var port in ports)
            {
                var widgets = CreatePortRow(port.PortId);
                _ports.AddChild(widgets.Root);
                _portWidgets.Add(port.PortId, widgets);
            }
        }

        _portsEmpty.Visible = ports.Count == 0;
        foreach (var port in ports)
        {
            var widgets = _portWidgets[port.PortId];
            widgets.Name.Text = port.DisplayName.ToUpperInvariant();
            widgets.Connection.Text = port.IsConnected
                ? $"{port.ConnectedObjectName} · {port.NetworkDisplayName}".Trim(' ', '·')
                : "FREI";
            widgets.Connection.AddThemeColorOverride("font_color",
                port.IsConnected ? BuildingUiTheme.Text : BuildingUiTheme.TextMuted);
            widgets.Output.Text = port.OutputPower > 0 ? FormatPower(port.OutputPower) : "—";
            widgets.Toggle.Visible = port.CanToggle;
            widgets.Toggle.ButtonPressed = port.IsEnabled;
            widgets.Toggle.Text = port.IsEnabled ? "AN" : "AUS";
            widgets.Disconnect.Disabled = !port.IsConnected || !port.CanDisconnect;
            widgets.Disconnect.Visible = port.IsConnected;
        }
    }

    private void RefreshSourceRows(IReadOnlyList<PowerSourceViewModel> sources)
    {
        var incomingIds = sources.Select(source => source.SourceId).ToArray();
        var existingIds = _sourceWidgets.Keys.ToArray();
        if (!incomingIds.SequenceEqual(existingIds, StringComparer.Ordinal))
        {
            foreach (var widgets in _sourceWidgets.Values)
            {
                widgets.Root.QueueFree();
            }

            _sourceWidgets.Clear();
            foreach (var source in sources)
            {
                var widgets = CreateSourceRow(source.SourceId);
                _sources.AddChild(widgets.Root);
                _sourceWidgets.Add(source.SourceId, widgets);
            }
        }

        _sourcesEmpty.Visible = sources.Count == 0;
        foreach (var source in sources)
        {
            var widgets = _sourceWidgets[source.SourceId];
            widgets.Name.Text = source.DisplayName.ToUpperInvariant();
            widgets.Output.Text = FormatPower(source.OutputPower);
            var fuel = Math.Max(0, source.FuelConsumptionPerMinute);
            widgets.Fuel.Text = fuel <= 0
                ? "OHNE TREIBSTOFF"
                : $"{fuel:0.00} E/min · {FormatRuntime(source.EstimatedFuelMinutesRemaining)}";
            widgets.Toggle.ButtonPressed = source.IsEnabled;
            widgets.Toggle.Text = source.IsEnabled ? "AN" : "AUS";
            widgets.Toggle.Disabled = !source.CanToggle;
        }
    }

    private PortWidgets CreatePortRow(string portId)
    {
        var root = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 38),
        };
        root.AddThemeConstantOverride("separation", 7);
        var name = new Label
        {
            CustomMinimumSize = new Vector2(74, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        name.AddThemeFontSizeOverride("font_size", 11);
        name.AddThemeColorOverride("font_color", BuildingUiTheme.Accent);
        root.AddChild(name);
        var connection = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        root.AddChild(connection);
        var output = new Label
        {
            CustomMinimumSize = new Vector2(74, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        output.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        root.AddChild(output);
        var toggle = new Button
        {
            ToggleMode = true,
            CustomMinimumSize = new Vector2(54, 30),
        };
        toggle.Toggled += enabled =>
        {
            if (!_suppressEvents)
            {
                PortEnabledChangeRequested?.Invoke(portId, enabled);
            }
        };
        root.AddChild(toggle);
        var disconnect = new Button
        {
            Text = "TRENNEN",
            CustomMinimumSize = new Vector2(82, 30),
        };
        disconnect.Pressed += () => DisconnectPortRequested?.Invoke(portId);
        root.AddChild(disconnect);
        return new PortWidgets(root, name, connection, output, toggle, disconnect);
    }

    private SourceWidgets CreateSourceRow(string sourceId)
    {
        var root = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 38),
        };
        root.AddThemeConstantOverride("separation", 7);
        var name = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        name.AddThemeFontSizeOverride("font_size", 11);
        name.AddThemeColorOverride("font_color", BuildingUiTheme.Accent);
        root.AddChild(name);
        var output = new Label
        {
            CustomMinimumSize = new Vector2(74, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        root.AddChild(output);
        var fuel = new Label
        {
            CustomMinimumSize = new Vector2(155, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        fuel.AddThemeFontSizeOverride("font_size", 10);
        fuel.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        root.AddChild(fuel);
        var toggle = new Button
        {
            ToggleMode = true,
            CustomMinimumSize = new Vector2(54, 30),
        };
        toggle.Toggled += enabled =>
        {
            if (!_suppressEvents)
            {
                SourceEnabledChangeRequested?.Invoke(sourceId, enabled);
            }
        };
        root.AddChild(toggle);
        return new SourceWidgets(root, name, output, fuel, toggle);
    }

    private void HandleNetworkToggled(bool enabled)
    {
        if (_suppressEvents)
        {
            return;
        }

        _networkToggle.Text = enabled ? "NETZ AN" : "NETZ AUS";
        NetworkEnabledChangeRequested?.Invoke(enabled);
    }

    private void SetMetric(string key, string text, Color? color = null)
    {
        var label = _metrics[key].Value;
        label.Text = text;
        label.AddThemeColorOverride("font_color", color ?? BuildingUiTheme.Text);
    }

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        var viewport = GetViewportRect().Size;
        _frame.CustomMinimumSize = new Vector2(
            Math.Clamp(viewport.X - 40, 360, 720),
            Math.Clamp(viewport.Y - 40, 300, 650));
        _metricGrid.Columns = viewport.X < 670 ? 2 : 4;
    }

    private static MarginContainer CreatePanelMargin()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        return margin;
    }

    private static HSeparator CreateSeparator() => new();

    private static string FormatPower(double value) => $"{Math.Max(0, value):0.#} kW";

    private static string FormatSignedPower(double value) =>
        value >= 0 ? $"+{value:0.#} kW" : $"{value:0.#} kW";

    private static string FormatRuntime(double? minutes)
    {
        if (minutes is null || !double.IsFinite(minutes.Value))
        {
            return "—";
        }

        var safeMinutes = Math.Max(0, minutes.Value);
        return safeMinutes >= 60
            ? $"{Math.Floor(safeMinutes / 60):0} h {safeMinutes % 60:0} min"
            : $"{safeMinutes:0.#} min";
    }

    private static string GetStatusText(PowerMenuNetworkStatus status) => status switch
    {
        PowerMenuNetworkStatus.Offline => "AUS",
        PowerMenuNetworkStatus.Stable => "STABIL",
        PowerMenuNetworkStatus.Limited => "KNAPP",
        PowerMenuNetworkStatus.Overloaded => "ÜBERLAST",
        PowerMenuNetworkStatus.CircuitBreakerTripped => "SCHUTZSCHALTER",
        _ => "UNBEKANNT",
    };

    private static Color GetStatusColor(PowerMenuNetworkStatus status) => status switch
    {
        PowerMenuNetworkStatus.Stable => BuildingUiTheme.Success,
        PowerMenuNetworkStatus.Limited => BuildingUiTheme.Warning,
        PowerMenuNetworkStatus.Overloaded or PowerMenuNetworkStatus.CircuitBreakerTripped =>
            BuildingUiTheme.Failure,
        _ => BuildingUiTheme.TextMuted,
    };

    private sealed record MetricWidgets(Label Value);

    private sealed record PortWidgets(
        HBoxContainer Root,
        Label Name,
        Label Connection,
        Label Output,
        Button Toggle,
        Button Disconnect);

    private sealed record SourceWidgets(
        HBoxContainer Root,
        Label Name,
        Label Output,
        Label Fuel,
        Button Toggle);
}

/// <summary>
/// Allocation-free chart redraw for the bounded 60-second history supplied by
/// the network UI model.
/// </summary>
public partial class PowerHistoryChartControl : Control
{
    private static readonly Color CapacityColor = new(0.13f, 0.79f, 0.98f);
    private static readonly Color ProductionColor = new(0.31f, 0.94f, 0.63f);
    private static readonly Color ConsumptionColor = new(1.0f, 0.63f, 0.2f);
    private static readonly Color DemandColor = new(0.88f, 0.42f, 0.92f);
    private IReadOnlyList<PowerHistorySampleViewModel> _samples = [];

    public override void _Ready() => Resized += QueueRedraw;

    public override void _ExitTree() => Resized -= QueueRedraw;

    public void SetSamples(IReadOnlyList<PowerHistorySampleViewModel> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        _samples = samples;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var plot = new Rect2(8, 24, Math.Max(1, Size.X - 16), Math.Max(1, Size.Y - 32));
        DrawRect(plot, new Color(0.002f, 0.016f, 0.022f, 0.9f), true);
        for (var row = 0; row <= 4; row++)
        {
            var y = plot.Position.Y + (plot.Size.Y * row / 4f);
            DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y),
                new Color(0.13f, 0.28f, 0.33f, 0.34f), 1, true);
        }

        DrawLegend(plot.Position.X);
        if (_samples.Count == 0)
        {
            DrawString(ThemeDB.FallbackFont, plot.GetCenter() + new Vector2(-52, 5),
                "NOCH KEINE MESSWERTE", HorizontalAlignment.Left, -1, 10,
                BuildingUiTheme.TextMuted);
            return;
        }

        var maximum = Math.Max(1, _samples.Max(sample => Math.Max(
            Math.Max(sample.Capacity, sample.ActualProduction),
            Math.Max(sample.ActualConsumption, sample.RequestedPower))));
        DrawSeries(plot, maximum, sample => sample.Capacity, CapacityColor);
        DrawSeries(plot, maximum, sample => sample.ActualProduction, ProductionColor);
        DrawSeries(plot, maximum, sample => sample.ActualConsumption, ConsumptionColor);
        DrawSeries(plot, maximum, sample => sample.RequestedPower, DemandColor);
    }

    private void DrawSeries(
        Rect2 plot,
        double maximum,
        Func<PowerHistorySampleViewModel, double> selector,
        Color color)
    {
        if (_samples.Count == 1)
        {
            var normalized = Math.Clamp(selector(_samples[0]) / maximum, 0, 1);
            DrawCircle(new Vector2(plot.End.X, plot.End.Y - ((float)normalized * plot.Size.Y)), 2, color);
            return;
        }

        var points = new Vector2[_samples.Count];
        for (var index = 0; index < _samples.Count; index++)
        {
            var x = plot.Position.X + (plot.Size.X * index / (_samples.Count - 1f));
            var normalized = Math.Clamp(selector(_samples[index]) / maximum, 0, 1);
            var y = plot.End.Y - ((float)normalized * plot.Size.Y);
            points[index] = new Vector2(x, y);
        }

        DrawPolyline(points, new Color(0, 0, 0, 0.65f), 4, true);
        DrawPolyline(points, color, 1.8f, true);
    }

    private void DrawLegend(float startX)
    {
        var entries = new[]
        {
            ("KAP.", CapacityColor),
            ("PROD.", ProductionColor),
            ("VERBR.", ConsumptionColor),
            ("BEDARF", DemandColor),
        };
        var x = startX;
        foreach (var (label, color) in entries)
        {
            DrawLine(new Vector2(x, 10), new Vector2(x + 12, 10), color, 2, true);
            DrawString(ThemeDB.FallbackFont, new Vector2(x + 16, 14), label,
                HorizontalAlignment.Left, -1, 9, BuildingUiTheme.TextMuted);
            x += label.Length * 6 + 32;
        }
    }
}
