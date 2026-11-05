namespace HomeSpeaker.Server2.Data;

/// <summary>A family member who keeps anchors.</summary>
public class AnchorPersonEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? Emoji { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The external feed's person this person is linked to, if any.</summary>
    public string? FeedPersonId { get; set; }

    public List<AnchorEntity> Anchors { get; set; } = [];
}
