using Godot;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.Info;

/// <summary>
/// Compact, pause-menu-owned knowledge overview. The page model deliberately
/// limits every section to one screen; detailed tutorials can be linked here
/// later without turning the overview into another scrolling settings page.
/// </summary>
public partial class InfoMenuController : CanvasLayer
{
    private static readonly InfoPageDefinition[] Pages =
    [
        new(
            "WERKZEUGE",
            "Ausrüstung wird in vier Werkzeugslots verwaltet. Der Hand-Slot spiegelt das aktuell gewählte Werkzeug.",
            [
                new("ERZ-ABBAUWERKZEUG", "Baut Erzquellen und endliche Erzsteine ab. Es funktioniert nur, wenn es als aktives Werkzeug ausgerüstet ist."),
                new("MASCHINEN-ABBAUWERKZEUG", "Zerlegt platzierte Maschinen, Kabel, Rohre, Förderbänder und Strommäste kontrolliert und gibt Material zurück."),
                new("WERKZEUGSLOTS", "Nur Gegenstände vom Typ Werkzeug passen in diese Slots. Leere Slots werden beim zyklischen Wechsel übersprungen."),
                new("HAND-SLOT", "Aktiviert das ausgewählte Werkzeug. Der Wechsel zu einem normalen Hotbar-Slot beendet den Hand-Modus."),
                new("SICHERER ABBRUCH", "Loslassen, Zielwechsel, zu grosse Distanz oder ein Slotwechsel brechen laufende Werkzeugaktionen ab."),
                new("TASTENBELEGUNG", "Alle Werkzeugaktionen und Slotwechsel lassen sich unter Tastatureinstellungen ändern."),
            ]),
        new(
            "MASCHINEN",
            "Maschinen sind datenbasiert aufgebaut und zeigen Rezept, Ein- und Ausgänge, Fortschritt, Energiebedarf und Status.",
            [
                new("WERKBANK", "Stellt frühe Werkzeuge und mobile Miner her. Produzierte Gegenstände landen im Ausgang der Maschine."),
                new("VERARBEITUNG", "Schmelzer, Zerkleinerer und Raffinerien wandeln Rohstoffe in nutzbare Zwischenprodukte um."),
                new("MONTAGE", "Montagemaschinen und Fabrikatoren kombinieren mehrere Komponenten zu komplexen Bauteilen."),
                new("MINER", "Mobile Miner arbeiten autark; automatische Miner benötigen Strom und werden direkt auf einer Erzquelle platziert."),
                new("STATUS", "Kein Strom, Material fehlt oder Ausgang voll erklären unmittelbar, warum eine Maschine wartet."),
                new("AUSGÄNGE", "Produkte bleiben physisch im Ausgang. Ein voller Ausgang stoppt die Produktion ohne Itemverlust."),
            ]),
        new(
            "RAUMSCHIFF",
            "Das Raumschiff verbindet Erkundung, Lager, Treibstoff und eine ankoppelbare Energiequelle.",
            [
                new("FLUG", "Normale Steuerung verändert die vorhandene Bewegung. Der Boost verbraucht die aktuell geladene Treibstoffart."),
                new("TREIBSTOFF", "Standardtreibstoff ist früh verfügbar; Hochleistungstreibstoff erhöht die Boost-Geschwindigkeit zusätzlich."),
                new("EIN- UND AUSSTIEG", "Die Interaktion ist nur an der Cockpitposition und bei einer sicheren Ausstiegsfläche verfügbar."),
                new("BEFESTIGUNG", "An grossen Kometen kann das Schiff bei niedriger Geschwindigkeit befestigt werden. Landebeine stabilisieren die Position."),
                new("LAGER", "Das Schiff besitzt ein separates Lager. In der Lageransicht können Gegenstände kontrolliert übertragen werden."),
                new("STROMANSCHLÜSSE", "Zwei Flügelanschlüsse können getrennte Netze versorgen, solange das Schiff sicher befestigt ist."),
            ]),
        new(
            "ITEMS",
            "Items besitzen eine zentrale Definition für Name, Icon, Typ, Stapelgrösse und besondere Verwendung.",
            [
                new("INVENTAR", "Das persönliche Inventar verwaltet Ressourcen und Bauteile; Werkzeuge besitzen eigene Slots."),
                new("HOTBAR", "Sechs Schnellzugriffsslots starten die Nutzung oder Platzierung des ausgewählten Gegenstands."),
                new("STAPEL", "Gleiche stapelbare Items werden bis zu ihrem Maximum zusammengeführt. Werkzeuge sind nicht stapelbar."),
                new("DRAG-AND-DROP", "Items lassen sich zwischen kompatiblen Slots verschieben, tauschen oder bewusst in die Welt fallen lassen."),
                new("BEHÄLTER", "Flüssigkeiten, Gase und Treibstoffe werden über kompatible Behälter in interne Tanks übertragen."),
                new("WELTOBJEKTE", "Gedroppte Items treiben mit sicher begrenzter Geschwindigkeit und können wieder eingesammelt werden."),
            ]),
        new(
            "FORSCHUNG",
            "Forschung steuert zentral, welche Maschinen, Rezepte und Produktionsstufen tatsächlich verfügbar sind.",
            [
                new("FREISCHALTUNGEN", "Gesperrte Inhalte bleiben im Baumenü und in Rezeptlisten verborgen, bis ihre Technologie erforscht ist."),
                new("AUTOMATISIERUNG", "Öffnet frühe Miner, Fördertechnik und Maschinen für kontinuierliche Produktionslinien."),
                new("ELEKTRONIK", "Schaltet Leiterplatten, Sensoren, Steuerungsmodule und präzisere Fertigung frei."),
                new("CHEMIE", "Erweitert die Fabrik um Gase, Flüssigkeiten, Kunststoffe und fortgeschrittene Treibstoffe."),
                new("ENERGIE", "Verbessert Quellen, Netze, Speicherung und später Hochenergie- beziehungsweise Nukleartechnik."),
                new("ENTDECKUNG", "Spezielle Ressourcen auf seltenen Kometen ergänzen die forschungsbasierte Progression."),
            ]),
        new(
            "ENERGIE",
            "Jede Kabelverbindung bildet mit ihren direkt und indirekt verbundenen Objekten ein eigenständiges Stromnetz.",
            [
                new("QUELLEN", "Generatoren und aktive Raumschiffanschlüsse addieren ihre verfügbare Leistung innerhalb desselben Netzes."),
                new("VERBRAUCHER", "Maschinen fordern nur dann Produktionsstrom an, wenn Rezept, Material und freier Ausgang vorhanden sind."),
                new("STROMMAST", "Ein kompakter Mast verbindet bis zu sechs Kabel und ermöglicht verzweigte oder getrennte Netze."),
                new("KABEL", "Jeder Anschluss nimmt höchstens ein Kabel auf. Länge und Anschlusstyp werden vor dem Bau geprüft."),
                new("SCHUTZSCHALTER", "Dauerhafte Überlastung schaltet das betroffene Netz ab. Es muss danach bewusst wieder aktiviert werden."),
                new("STROMMENÜ", "Generator, Strommast und Raumschiff zeigen Netzstatus, Bedarf, Reserve, Anschlüsse und den 60-Sekunden-Verlauf."),
            ]),
        new(
            "PRODUKTION",
            "Mehrstufige Rezepte verbinden Abbau, Verarbeitung, Montage, Energie und Logistik zu skalierbaren Fabriken.",
            [
                new("REZEPTE", "Jedes Rezept definiert Eingaben, Ausgaben, Dauer, Energiebedarf und die dafür geeigneten Maschinen."),
                new("EINGÄNGE", "Produktion beginnt nur mit allen erforderlichen Materialien in den vorgesehenen Maschinenslots."),
                new("AUSGÄNGE", "Endprodukte und Nebenprodukte belegen getrennte Ausgänge; blockierte Ausgänge stoppen die Maschine."),
                new("LOGISTIK", "Förderbänder transportieren Feststoffe, Rohre bewegen Flüssigkeiten und Gase, Kabel verteilen Energie."),
                new("SKALIERUNG", "Getrennte Linien und Stromnetze machen Engpässe sichtbar und erlauben gezielte Erweiterungen."),
                new("PERSISTENZ", "Maschinenzustände, Fortschritt, Inventare und Verbindungen bleiben über Chunk-Wechsel und Laden erhalten."),
            ]),
    ];

    private readonly List<Button> _navigationButtons = [];
    private readonly List<PanelContainer> _knowledgeCards = [];
    private MarginContainer _safeArea = null!;
    private MarginContainer _frameMargin = null!;
    private PanelContainer _frame = null!;
    private PanelContainer _navigationPanel = null!;
    private VBoxContainer _navigationList = null!;
    private Label _title = null!;
    private Label _sectionTitle = null!;
    private Label _introduction = null!;
    private GridContainer _cardGrid = null!;
    private Label _pageStatus = null!;
    private Button _backButton = null!;
    private int _selectedPageIndex;
    private bool _ready;

    public bool IsOpen => Visible;

    public int SectionCount => Pages.Length;

    public string ActiveSectionName => Pages[_selectedPageIndex].Title;

    public event Action? BackRequested;

    public override void _Ready()
    {
        _safeArea = GetNode<MarginContainer>("InputBlocker/SafeArea");
        _frame = GetNode<PanelContainer>("InputBlocker/SafeArea/Frame");
        _frameMargin = GetNode<MarginContainer>("InputBlocker/SafeArea/Frame/FrameMargin");
        _navigationPanel = GetNode<PanelContainer>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Body/NavigationPanel");
        _navigationList = GetNode<VBoxContainer>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Body/NavigationPanel/Margin/Layout/NavigationList");
        _title = GetNode<Label>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Header/Title");
        _sectionTitle = GetNode<Label>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Body/ContentPanel/Margin/Content/SectionTitle");
        _introduction = GetNode<Label>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Body/ContentPanel/Margin/Content/Introduction");
        _cardGrid = GetNode<GridContainer>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Body/ContentPanel/Margin/Content/Cards");
        _pageStatus = GetNode<Label>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Footer/PageStatus");
        _backButton = GetNode<Button>("InputBlocker/SafeArea/Frame/FrameMargin/Layout/Footer/Back");

        _safeArea.Theme = SettingsMenuController.CreateSciFiTheme();
        _frame.AddThemeStyleboxOverride(
            "panel",
            CreateStyleBox(new Color(0.004f, 0.022f, 0.034f, 0.98f), new Color(0.04f, 0.66f, 0.86f, 0.9f), 2));
        _navigationPanel.AddThemeStyleboxOverride(
            "panel",
            CreateStyleBox(new Color(0.008f, 0.045f, 0.064f, 0.94f), new Color(0.08f, 0.42f, 0.54f, 0.9f), 1));

        BuildNavigation();
        _backButton.Pressed += ReturnToPauseMenu;
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _ready = true;
        SelectPage(0, focusNavigation: false);
        UpdateResponsiveLayout();
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_ready)
        {
            _backButton.Pressed -= ReturnToPauseMenu;
            GetViewport().SizeChanged -= UpdateResponsiveLayout;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        var isEscape = @event is InputEventKey { Pressed: true, Echo: false } keyEvent &&
            (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape);
        var isPauseAction = @event.IsActionPressed("pause") &&
            (@event is not InputEventKey pauseKey || !pauseKey.Echo);
        if (!isEscape && !isPauseAction)
        {
            return;
        }

        ReturnToPauseMenu();
        GetViewport().SetInputAsHandled();
    }

    public void Open()
    {
        SelectPage(0, focusNavigation: false);
        UpdateResponsiveLayout();
        Visible = true;
        _navigationButtons[0].GrabFocus();
    }

    public void CloseImmediately()
    {
        Visible = false;
        GetViewport().GuiReleaseFocus();
    }

#if DEBUG
    public void RunConstructionSmokeTest()
    {
        if (!_ready || Pages.Length != 7 || _navigationButtons.Count != Pages.Length)
        {
            throw new InvalidOperationException("Info menu did not construct all seven knowledge sections.");
        }

        if (ContainsScrollContainer(GetNode("InputBlocker/SafeArea/Frame")))
        {
            throw new InvalidOperationException("Info overview must not require a ScrollContainer.");
        }

        Vector2[] desktopSizes = [new(1366, 768), new(1920, 1080), new(2560, 1440)];
        foreach (var viewportSize in desktopSizes)
        {
            ValidateDesktopMetrics(viewportSize, CalculateResponsiveMetrics(viewportSize));
        }

        var wasVisible = Visible;
        for (var pageIndex = 0; pageIndex < Pages.Length; pageIndex++)
        {
            SelectPage(pageIndex, focusNavigation: false);
            if (_knowledgeCards.Count != Pages[pageIndex].Cards.Length ||
                _sectionTitle.Text != Pages[pageIndex].Title)
            {
                throw new InvalidOperationException($"Info section '{Pages[pageIndex].Title}' is incomplete.");
            }
        }

        SelectPage(0, focusNavigation: false);
        Visible = wasVisible;
        GD.Print("INFO_MENU_SMOKE_OK: 7 sections, no scrolling, 1366/1920/2560 responsive layouts");
    }
#endif

    private void BuildNavigation()
    {
        for (var pageIndex = 0; pageIndex < Pages.Length; pageIndex++)
        {
            var capturedIndex = pageIndex;
            var button = new Button
            {
                Text = Pages[pageIndex].Title,
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.All,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                ToggleMode = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.Pressed += () => SelectPage(capturedIndex, focusNavigation: false);
            _navigationList.AddChild(button);
            _navigationButtons.Add(button);
        }
    }

    private void SelectPage(int pageIndex, bool focusNavigation)
    {
        _selectedPageIndex = Math.Clamp(pageIndex, 0, Pages.Length - 1);
        var page = Pages[_selectedPageIndex];
        _sectionTitle.Text = page.Title;
        _introduction.Text = page.Introduction;
        _pageStatus.Text = $"{_selectedPageIndex + 1:D2} / {Pages.Length:D2}";

        for (var index = 0; index < _navigationButtons.Count; index++)
        {
            _navigationButtons[index].ButtonPressed = index == _selectedPageIndex;
        }

        ClearKnowledgeCards();
        foreach (var card in page.Cards)
        {
            AddKnowledgeCard(card);
        }

        UpdateResponsiveLayout();
        if (focusNavigation && Visible)
        {
            _navigationButtons[_selectedPageIndex].GrabFocus();
        }
    }

    private void ClearKnowledgeCards()
    {
        _knowledgeCards.Clear();
        foreach (var child in _cardGrid.GetChildren())
        {
            _cardGrid.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void AddKnowledgeCard(KnowledgeCard definition)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            CreateStyleBox(new Color(0.01f, 0.055f, 0.074f, 0.92f), new Color(0.08f, 0.38f, 0.5f, 0.8f), 1));
        var margin = new MarginContainer();
        SetMargins(margin, 12, 10);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 5);
        var heading = new Label
        {
            Text = definition.Title,
            ThemeTypeVariation = "InfoCardTitle",
        };
        heading.AddThemeColorOverride("font_color", new Color(0.22f, 0.84f, 1));
        var body = new Label
        {
            Text = definition.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Top,
        };
        layout.AddChild(heading);
        layout.AddChild(body);
        margin.AddChild(layout);
        panel.AddChild(margin);
        _cardGrid.AddChild(panel);
        _knowledgeCards.Add(panel);
    }

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        ApplyResponsiveMetrics(CalculateResponsiveMetrics(GetViewport().GetVisibleRect().Size));
    }

    private void ApplyResponsiveMetrics(ResponsiveMetrics metrics)
    {
        SetMargins(_safeArea, metrics.SafeMarginX, metrics.SafeMarginY);
        SetMargins(_frameMargin, metrics.FrameMarginX, metrics.FrameMarginY);
        _navigationPanel.CustomMinimumSize = new Vector2(metrics.NavigationWidth, 0);
        _title.AddThemeFontSizeOverride("font_size", metrics.TitleFontSize);
        _sectionTitle.AddThemeFontSizeOverride("font_size", metrics.SectionTitleFontSize);
        _introduction.AddThemeFontSizeOverride("font_size", metrics.BodyFontSize);
        _navigationList.AddThemeConstantOverride("separation", metrics.NavigationGap);
        _cardGrid.AddThemeConstantOverride("h_separation", metrics.CardGap);
        _cardGrid.AddThemeConstantOverride("v_separation", metrics.CardGap);
        foreach (var button in _navigationButtons)
        {
            button.CustomMinimumSize = new Vector2(0, metrics.NavigationButtonHeight);
            button.AddThemeFontSizeOverride("font_size", metrics.NavigationFontSize);
        }

        foreach (var card in _knowledgeCards)
        {
            card.CustomMinimumSize = new Vector2(0, metrics.CardHeight);
        }
    }

    private void ReturnToPauseMenu()
    {
        CloseImmediately();
        BackRequested?.Invoke();
    }

    private static ResponsiveMetrics CalculateResponsiveMetrics(Vector2 viewportSize)
    {
        var width = Mathf.Max(1_024, viewportSize.X);
        var height = Mathf.Max(700, viewportSize.Y);
        var compact = width < 1_600 || height < 900;
        return new ResponsiveMetrics(
            SafeMarginX: Mathf.RoundToInt(Mathf.Clamp(width * 0.028f, 28, 72)),
            SafeMarginY: Mathf.RoundToInt(Mathf.Clamp(height * 0.028f, 18, 46)),
            FrameMarginX: compact ? 20 : 30,
            FrameMarginY: compact ? 16 : 24,
            NavigationWidth: compact ? 218 : 270,
            NavigationButtonHeight: compact ? 46 : 56,
            NavigationGap: compact ? 7 : 10,
            NavigationFontSize: compact ? 15 : 18,
            TitleFontSize: compact ? 30 : 38,
            SectionTitleFontSize: compact ? 27 : 34,
            BodyFontSize: compact ? 15 : 17,
            CardHeight: compact ? 90 : height < 1_200 ? 112 : 126,
            CardGap: compact ? 9 : 13);
    }

    private static void ValidateDesktopMetrics(Vector2 viewportSize, ResponsiveMetrics metrics)
    {
        var usableHeight = viewportSize.Y - (2 * metrics.SafeMarginY) - (2 * metrics.FrameMarginY);
        var cardRowsHeight = (3 * metrics.CardHeight) + (2 * metrics.CardGap);
        const float fixedVerticalContent = 188;
        if (metrics.NavigationWidth < 200 ||
            metrics.NavigationButtonHeight * Pages.Length + metrics.NavigationGap * (Pages.Length - 1) > usableHeight - 46 ||
            cardRowsHeight + fixedVerticalContent > usableHeight)
        {
            throw new InvalidOperationException(
                $"Info layout does not fit {viewportSize.X:0}x{viewportSize.Y:0} without scrolling.");
        }
    }

    private static bool ContainsScrollContainer(Node node)
    {
        if (node is ScrollContainer)
        {
            return true;
        }

        foreach (var child in node.GetChildren())
        {
            if (ContainsScrollContainer(child))
            {
                return true;
            }
        }

        return false;
    }

    private static StyleBoxFlat CreateStyleBox(Color background, Color border, int borderWidth) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };

    private static void SetMargins(MarginContainer container, int horizontal, int vertical)
    {
        container.AddThemeConstantOverride("margin_left", horizontal);
        container.AddThemeConstantOverride("margin_right", horizontal);
        container.AddThemeConstantOverride("margin_top", vertical);
        container.AddThemeConstantOverride("margin_bottom", vertical);
    }

    private sealed record InfoPageDefinition(string Title, string Introduction, KnowledgeCard[] Cards);

    private sealed record KnowledgeCard(string Title, string Description);

    private readonly record struct ResponsiveMetrics(
        int SafeMarginX,
        int SafeMarginY,
        int FrameMarginX,
        int FrameMarginY,
        int NavigationWidth,
        int NavigationButtonHeight,
        int NavigationGap,
        int NavigationFontSize,
        int TitleFontSize,
        int SectionTitleFontSize,
        int BodyFontSize,
        int CardHeight,
        int CardGap);
}
