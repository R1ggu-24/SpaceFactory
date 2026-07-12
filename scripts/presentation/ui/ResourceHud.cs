using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.UI;

public partial class ResourceHud : CanvasLayer
{
    private Label _interaction = null!;
    private PanelContainer _miningPanel = null!;
    private Label _resourceName = null!;
    private ProgressBar _progress = null!;
    private Label _inventory = null!;
    private Label _message = null!;
    private double _messageRemaining;
    private string? _shipPrompt;
    private string? _resourcePrompt;

    public override void _Ready()
    {
        _interaction = GetNode<Label>("Interaction");
        _miningPanel = GetNode<PanelContainer>("MiningPanel");
        _resourceName = GetNode<Label>("MiningPanel/Content/ResourceName");
        _progress = GetNode<ProgressBar>("MiningPanel/Content/Progress");
        _inventory = GetNode<Label>("InventoryPanel/Content/Items");
        _message = GetNode<Label>("Message");
        _interaction.Visible = false;
        _miningPanel.Visible = false;
        _message.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_messageRemaining <= 0)
        {
            return;
        }

        _messageRemaining -= delta;
        if (_messageRemaining <= 0)
        {
            _message.Visible = false;
        }
    }

    public void SetInteractionPrompt(string? text)
    {
        _shipPrompt = text;
        RefreshInteractionPrompt();
    }

    public void SetResourcePrompt(string? text)
    {
        _resourcePrompt = text;
        RefreshInteractionPrompt();
    }

    public void SetMining(string? resourceName, float progress)
    {
        var visible = !string.IsNullOrWhiteSpace(resourceName);
        _miningPanel.Visible = visible;
        RefreshInteractionPrompt();
        if (!visible)
        {
            _progress.Value = 0;
            return;
        }

        _resourceName.Text = $"Abbau: {resourceName}";
        _progress.Value = Mathf.Clamp(progress, 0, 1) * 100;
    }

    private void RefreshInteractionPrompt()
    {
        var text = _resourcePrompt ?? _shipPrompt;
        _interaction.Text = text ?? string.Empty;
        _interaction.Visible = !_miningPanel.Visible && !string.IsNullOrWhiteSpace(text);
    }

    public void ShowMessage(string text, double seconds = 2.2)
    {
        _message.Text = text;
        _message.Visible = true;
        _messageRemaining = seconds;
    }

    public void UpdateInventory(SlotInventory inventory, IReadOnlyList<ResourceDefinition> resources)
    {
        var lines = resources
            .Select(resource => (Resource: resource, Amount: inventory.GetAmount(resource.Id)))
            .Where(entry => entry.Amount > 0)
            .OrderBy(entry => entry.Resource.DisplayName)
            .Select(entry => $"{entry.Resource.DisplayName}: {entry.Amount}/{inventory.MaximumStackSize}")
            .ToArray();
        _inventory.Text = lines.Length == 0
            ? $"Leer\nKapazität: {inventory.UsedCapacity}/{inventory.Capacity}"
            : $"{string.Join("\n", lines)}\n\nKapazität: {inventory.UsedCapacity}/{inventory.Capacity}";
    }
}
