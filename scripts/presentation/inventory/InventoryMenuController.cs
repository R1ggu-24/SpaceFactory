using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventoryMenuController : CanvasLayer
{
    public const string AstronautInventoryId = "astronaut";
    public const string ShipInventoryId = "ship";

    private InventoryPanelControl _astronautPanel = null!;
    private InventoryPanelControl _shipPanel = null!;
    private BoxContainer _inventoryColumns = null!;
    private Label _mode = null!;
    private Label _status = null!;
    private SlotInventory? _astronautInventory;
    private SlotInventory? _shipInventory;
    private IReadOnlyDictionary<string, ResourceDefinition>? _resources;
    private bool _ready;
    private bool _initialized;

    public bool IsOpen => Visible;

    public bool IsShipStorageVisible => Visible && _shipPanel.Visible;

    public event Action? Closed;

    public override void _Ready()
    {
        _astronautPanel = GetNode<InventoryPanelControl>("SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/AstronautInventory");
        _shipPanel = GetNode<InventoryPanelControl>("SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/ShipInventory");
        _inventoryColumns = GetNode<BoxContainer>("SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns");
        _mode = GetNode<Label>("SafeArea/Frame/FrameMargin/Layout/Header/Mode");
        _status = GetNode<Label>("SafeArea/Frame/FrameMargin/Layout/Footer/Status");
        GetNode<Control>("SafeArea").Theme = CreateSciFiTheme();
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _ready = true;
        if (_astronautInventory is not null && _shipInventory is not null && _resources is not null)
        {
            ConfigurePanels();
        }

        UpdateResponsiveLayout();
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_ready)
        {
            GetViewport().SizeChanged -= UpdateResponsiveLayout;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        var key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
        if (key != Key.I && key != Key.Escape && !@event.IsActionPressed("inventory"))
        {
            return;
        }

        Close();
        GetViewport().SetInputAsHandled();
    }

    public void Initialize(
        SlotInventory astronautInventory,
        SlotInventory shipInventory,
        IReadOnlyList<ResourceDefinition> resources)
    {
        ArgumentNullException.ThrowIfNull(astronautInventory);
        ArgumentNullException.ThrowIfNull(shipInventory);
        ArgumentNullException.ThrowIfNull(resources);
        if (astronautInventory.SlotCount != InventoryConfiguration.AstronautSlotCount)
        {
            throw new ArgumentException(
                $"Astronaut inventory must contain {InventoryConfiguration.AstronautSlotCount} slots.",
                nameof(astronautInventory));
        }

        if (shipInventory.SlotCount != InventoryConfiguration.ShipSlotCount)
        {
            throw new ArgumentException(
                $"Ship inventory must contain {InventoryConfiguration.ShipSlotCount} slots.",
                nameof(shipInventory));
        }

        _astronautInventory = astronautInventory;
        _shipInventory = shipInventory;
        _resources = resources.ToDictionary(resource => resource.Id.Value, StringComparer.Ordinal);
        if (_ready)
        {
            ConfigurePanels();
        }
    }

    /// <summary>
    /// Opens only the personal inventory. Ship storage stays inaccessible.
    /// </summary>
    public void OpenAstronautInventory() => Open(includeShipStorage: false);

    /// <summary>
    /// Opens both inventories. The caller must only use this while controlling the ship.
    /// </summary>
    public void OpenShipInventory() => Open(includeShipStorage: true);

    public void Open(bool includeShipStorage)
    {
        EnsureInitialized();
        _shipPanel.Visible = includeShipStorage;
        _mode.Text = includeShipStorage ? "RAUMSCHIFF • TRANSFERMODUS" : "ASTRONAUT • PERSÖNLICH";
        _status.Text = includeShipStorage
            ? "Stapel mit der linken Maustaste zwischen den Inventaren ziehen."
            : "Das Raumschifflager ist nur innerhalb des Raumschiffs verfügbar.";
        Refresh();
        UpdateResponsiveLayout();
        Visible = true;
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        GetViewport().GuiReleaseFocus();
        Closed?.Invoke();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            return;
        }

        _astronautPanel.Refresh();
        _shipPanel.Refresh();
    }

    private void ConfigurePanels()
    {
        _astronautPanel.Configure(
            AstronautInventoryId,
            "ASTRONAUTEN-INVENTAR",
            5,
            _astronautInventory!,
            _resources!,
            TransferStack);
        _shipPanel.Configure(
            ShipInventoryId,
            "RAUMSCHIFFLAGER",
            10,
            _shipInventory!,
            _resources!,
            TransferStack);
        _initialized = true;
    }

    private bool TransferStack(InventorySlotAddress source, InventorySlotAddress target)
    {
        if (!TryGetInventory(source.InventoryId, out var sourceInventory) ||
            !TryGetInventory(target.InventoryId, out var targetInventory))
        {
            _status.Text = "Transfer abgelehnt: unbekanntes Inventar.";
            return false;
        }

        if (!_shipPanel.Visible &&
            (source.InventoryId == ShipInventoryId || target.InventoryId == ShipInventoryId))
        {
            _status.Text = "Das Raumschifflager ist momentan nicht zugänglich.";
            return false;
        }

        var result = InventoryTransfer.Transfer(
            sourceInventory,
            source.SlotIndex,
            targetInventory,
            target.SlotIndex);
        Refresh();
        _status.Text = DescribeTransfer(result);
        return result.Succeeded;
    }

    private bool TryGetInventory(string inventoryId, out SlotInventory inventory)
    {
        inventory = inventoryId switch
        {
            AstronautInventoryId when _astronautInventory is not null => _astronautInventory,
            ShipInventoryId when _shipInventory is not null => _shipInventory,
            _ => null!,
        };
        return inventory is not null;
    }

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        // At narrow aspect ratios both panels stack and remain reachable through
        // the outer ScrollContainer. At common desktop widths they stay side by side.
        var shouldStack = GetViewport().GetVisibleRect().Size.X < 1260;
        _inventoryColumns.Set("vertical", shouldStack);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("InventoryMenuController.Initialize must be called before opening the menu.");
        }
    }

    private static string DescribeTransfer(InventoryTransferResult result)
    {
        if (result.Succeeded)
        {
            if (result.MovedAmount == 0)
            {
                return "Quelle und Ziel sind identisch.";
            }

            var action = result.Swapped ? "Stapel getauscht" : "Transfer erfolgreich";
            var remainder = result.RemainingAmount > 0 ? $" • {result.RemainingAmount} verbleiben" : string.Empty;
            return $"{action}: {result.MovedAmount} Einheiten{remainder}.";
        }

        return result.Failure switch
        {
            InventoryTransferFailure.SourceEmpty => "Transfer nicht möglich: Quellslot ist leer.",
            InventoryTransferFailure.TargetStackFull => "Transfer nicht möglich: Zielstapel ist voll.",
            InventoryTransferFailure.IncompatibleStacks => "Transfer nicht möglich: Stapel sind nicht kompatibel.",
            InventoryTransferFailure.StackLimitExceeded => "Transfer nicht möglich: maximal 200 Einheiten je Slot.",
            InventoryTransferFailure.InsufficientItems => "Transfer nicht möglich: zu wenige Einheiten vorhanden.",
            _ => "Transfer konnte nicht ausgeführt werden.",
        };
    }

    private static Theme CreateSciFiTheme()
    {
        var theme = new Theme();
        theme.SetColor("font_color", "Label", new Color(0.78f, 0.9f, 0.95f));
        theme.SetFontSize("font_size", "Label", 15);
        theme.SetStylebox("panel", "PanelContainer", new StyleBoxFlat
        {
            BgColor = new Color(0.003f, 0.02f, 0.032f, 0.96f),
            BorderColor = new Color(0.04f, 0.42f, 0.56f, 0.9f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        });
        return theme;
    }
}
