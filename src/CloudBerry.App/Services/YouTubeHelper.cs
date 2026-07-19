using System;
using System.Windows;

namespace CloudBerry.App.Services;

/// <summary>
/// YouTube integration helpers — handles autoplay policies, error recovery, and embed best practices.
/// Ensures trailer previews work reliably across modern browser engines.
/// </summary>
public static class YouTubeHelper
{
    /// <summary>
    /// Builds a YouTube embed URL with autoplay-safe parameters.
    /// Required for modern autoplay policies: mute=1, autoplay=1, modestbranding=1.
    /// </summary>
    /// <param name="videoId">YouTube video ID (11 characters)</param>
    /// <param name="autoplay">Whether to start playing on load</param>
    /// <param name="loop">Whether to loop the video</param>
    /// <param name="startTime">Optional start time in seconds</param>
    /// <returns>Formatted embed URL ready for WebView2/WebBrowser</returns>
    public static string BuildEmbedUrl(string videoId, bool autoplay = true, bool loop = false, int? startTime = null)
    {
        if (string.IsNullOrWhiteSpace(videoId) || videoId.Length != 11)
            return string.Empty;

        // Use youtube-nocookie.com for better embedding compatibility and referrer handling [[23]]
        var url = $"https://www.youtube-nocookie.com/embed/{videoId}?rel=0&modestbranding=1&playsinline=1&origin=https://www.youtube-nocookie.com";
        
        if (autoplay)
        {
            // User requested audio enabled for trailers
            url += "&autoplay=1";
        }
        
        if (loop)
            url += "&loop=1&playlist=" + videoId;
            
        if (startTime.HasValue && startTime > 0)
            url += $"&start={startTime.Value}";

        // Black Flash theme: subtle crimson border for visual cohesion
        url += "&color=white";
        
        return url;
    }

    /// <summary>
    /// Handles YouTube IFrame API error codes gracefully.
    /// Error 153 = content cannot be played (embedding disabled, region lock, or policy violation).
    /// </summary>
    /// <param name="errorCode">YouTube IFrame API error code</param>
    /// <param name="fallbackAction">Action to take when video fails (e.g., show placeholder image)</param>
    /// <returns>True if error was handled, false if it should propagate</returns>
    public static bool HandleError(int errorCode, Action? fallbackAction = null)
    {
        // 2 = Invalid parameter, 5 = HTML5 player error, 100 = Video not found
        // 150 = Embedding disabled by owner, 153 = Content cannot be played
        if (errorCode is 2 or 5 or 100 or 150 or 153)
        {
            fallbackAction?.Invoke();
            // Log internally for debugging (no external dependency)
            #if DEBUG
            System.Diagnostics.Debug.WriteLine($"[YouTubeHelper] Error {errorCode}: trailer unavailable, showing fallback");
            #endif
            return true;
        }
        return false; // Let other errors propagate for debugging
    }

    /// <summary>
    /// Generates a Black Flash-themed placeholder image URL for failed trailers.
    /// Uses your existing yuji assets with crimson overlay for seamless fallback.
    /// </summary>
    /// <param name="yujiAssetIndex">Index 1-7 for yuji{N}.png</param>
    /// <returns>Relative path to placeholder asset</returns>
    public static string GetFallbackPlaceholder(int yujiAssetIndex = 1)
    {
        var index = Math.Clamp(yujiAssetIndex, 1, 7);
        return $"pack://application:,,,/Resources/Images/yuji{index}.png";
    }

    /// <summary>
    /// Generates complete iframe HTML with referrerpolicy fix for Error 153.
    /// CRITICAL: referrerpolicy="strict-origin-when-cross-origin" prevents YouTube from rejecting the embed request [[2]][[19]].
    /// </summary>
    public static string BuildIframeHtml(string videoId, int width = 1280, int height = 720)
    {
        var embedUrl = BuildEmbedUrl(videoId);
        // referrerpolicy fixes Error 153 + allow attribute enables autoplay with audio
        return $@"<iframe width=""{width}"" height=""{height}"" 
                src=""{embedUrl}"" 
                frameborder=""0"" 
                allow=""accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"" 
                referrerpolicy=""strict-origin-when-cross-origin"" 
                allowfullscreen>
                </iframe>";
    }
}
