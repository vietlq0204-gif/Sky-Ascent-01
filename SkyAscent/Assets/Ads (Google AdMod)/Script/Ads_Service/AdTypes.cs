using System;

public enum AdProviderType
{
    None,
    AdMob,
}

public enum AdFormatType
{
    None,
    Banner,
    Interstitial,
    Rewarded,
    RewardedInterstitial,
    AppOpen,
    NativeOverlay,
}

public enum BannerAdSizeType
{
    Banner,
    LargeBanner,
    IABBanner,
    Leaderboard,
    MediumRectangle,
    AnchoredAdaptive,
}

public enum BannerScreenPositionType
{
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center,
}


public enum AdServiceResultType
{
    None,
    Loaded,
    FailedToLoad,
    Shown,
    FailedToShow,
    Closed,
    Hidden,
    Destroyed,
    RewardEarned,
    Skipped,
    Disabled,
    NotReady,
    Busy,
    SdkUnavailable,
}

public sealed class AdServiceResult
{
    public AdServiceResult(
        AdFormatType format,
        AdServiceResultType resultType,
        string message = "",
        bool rewardEarned = false)
    {
        Format = format;
        ResultType = resultType;
        Message = message ?? string.Empty;
        RewardEarned = rewardEarned;
    }

    public AdFormatType Format { get; }
    public AdServiceResultType ResultType { get; }
    public string Message { get; }
    public bool RewardEarned { get; }
}
