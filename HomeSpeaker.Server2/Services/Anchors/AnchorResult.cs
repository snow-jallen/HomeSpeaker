namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>How an anchor change went; the API maps each to a status code.</summary>
public enum AnchorResult
{
    Ok,
    NotFound,
    Invalid,

    /// <summary>The anchor belongs to another source and can't be changed (or ticked) from here.</summary>
    ReadOnly,

    /// <summary>The anchor home or feed couldn't be reached.</summary>
    Unavailable,
}
