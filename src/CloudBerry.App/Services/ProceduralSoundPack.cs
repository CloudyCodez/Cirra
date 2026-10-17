using System.IO;
using System.Media;

namespace CloudBerry.App.Services;

internal static class ProceduralSoundPack
{
    public static void PlayCue(string cue, string profile, string theme, int variant)
    {
        var (frequencies, durationMs, amplitude) = ResolveCue(cue, profile, theme, variant);
        using var stream = BuildWaveStream(frequencies, durationMs, amplitude);
        using var player = new SoundPlayer(stream);
        player.PlaySync();
    }

    private static (double[] frequencies, int durationMs, double amplitude) ResolveCue(string cue, string profile, string theme, int variant)
    {
        var subtle = string.Equals(profile, "Subtle", StringComparison.OrdinalIgnoreCase);
        var cinematic = string.Equals(profile, "Cinematic", StringComparison.OrdinalIgnoreCase);
        var digital = string.Equals(profile, "Digital", StringComparison.OrdinalIgnoreCase);
        var themeSync = string.Equals(profile, "Theme Sync", StringComparison.OrdinalIgnoreCase);
        var amp = subtle ? 0.16 : cinematic ? 0.27 : digital ? 0.19 : themeSync ? 0.23 : 0.21;
        var normalizedTheme = (theme ?? string.Empty).Trim().ToLowerInvariant();

        if (themeSync || normalizedTheme is "cirra core" or "archive ivory" or "signal rain" or "arcade static" or "tidal glass" or "midnight observatory" or "verdant glass" or "high contrast" or "impact noir")
        {
            return ResolveThemeSyncedCue(cue, normalizedTheme, variant, amp, cinematic);
        }

        return cue switch
        {
            "app.boot" => (variant % 2 == 0 ? [220.00, 329.63, 493.88] : [246.94, 369.99, 554.37], cinematic ? 520 : 460, amp * 0.9),
            "app.ready" => (variant % 2 == 0 ? [554.37, 739.99] : [523.25, 698.46], cinematic ? 175 : 145, amp),
            "menu.open" => (variant % 2 == 0 ? [659.25] : [698.46], 95, amp),
            "tab.switch" => (variant % 2 == 0 ? [466.16] : [493.88], 55, amp * 0.95),
            "ui.focus" => (digital ? [698.46] : [523.25], 48, amp * 0.72),
            "ui.click" => (variant % 3 == 0 ? [415.30] : variant % 3 == 1 ? [392.00] : [440.00], 42, amp * 0.85),
            "ui.confirm" => (variant % 2 == 0 ? [587.33, 739.99] : [622.25, 783.99], 120, amp),
            "ui.success" => (variant % 2 == 0 ? [523.25, 659.25, 783.99] : [554.37, 698.46, 830.61], 165, amp),
            "game.launch" => (variant % 2 == 0 ? [392.00, 493.88, 659.25] : [415.30, 523.25, 698.46], cinematic ? 230 : 185, amp),
            "game.launch.handoff" => (digital ? [311.13, 466.16, 698.46] : [349.23, 523.25, 783.99], cinematic ? 280 : 230, amp),
            "metadata.apply" => (variant % 2 == 0 ? [659.25, 830.61] : [622.25, 783.99], 135, amp * 0.94),
            "achievement.unlock" => (digital ? [523.25, 659.25, 1046.50] : [493.88, 659.25, 987.77], cinematic ? 240 : 205, amp),
            "ui.warning" => ([369.99, 329.63], 165, amp),
            "ui.error" => ([293.66, 246.94], 180, amp),
            _ => ([440.00], 50, amp * 0.8)
        };
    }

    private static (double[] frequencies, int durationMs, double amplitude) ResolveThemeSyncedCue(string cue, string theme, int variant, double amplitude, bool cinematic)
    {
        return theme switch
        {
            "cirra core" => cue switch
            {
                "app.boot" => ([196.00, 293.66, 440.00, 659.25], cinematic ? 600 : 510, amplitude * 0.84),
                "app.ready" => ([440.00, 659.25], 130, amplitude * 0.76),
                "menu.open" => ([329.63, 493.88], 78, amplitude * 0.68),
                "tab.switch" => ([392.00, 587.33], 48, amplitude * 0.6),
                "ui.focus" => ([659.25], 34, amplitude * 0.48),
                "ui.click" => ([493.88], 30, amplitude * 0.5),
                "ui.confirm" => ([440.00, 659.25], 92, amplitude * 0.72),
                "ui.success" => ([392.00, 587.33, 783.99], 148, amplitude * 0.78),
                "metadata.apply" => ([523.25, 698.46], 112, amplitude * 0.72),
                "achievement.unlock" => ([440.00, 659.25, 987.77], 220, amplitude * 0.86),
                "game.launch.handoff" => ([246.94, 369.99, 554.37, 830.61], cinematic ? 300 : 235, amplitude * 0.82),
                "ui.warning" => ([349.23, 293.66], 132, amplitude * 0.66),
                "ui.error" => ([293.66, 220.00], 150, amplitude * 0.68),
                _ => ([493.88], 34, amplitude * 0.52)
            },
            "signal rain" => cue switch
            {
                "app.boot" => ([164.81, 246.94, 369.99], cinematic ? 560 : 490, amplitude * 0.94),
                "ui.focus" => ([739.99], 42, amplitude * 0.72),
                "metadata.apply" => ([659.25, 987.77], 125, amplitude * 0.92),
                "achievement.unlock" => ([523.25, 783.99, 1174.66], 215, amplitude),
                "game.launch.handoff" => ([329.63, 493.88, 739.99], cinematic ? 290 : 235, amplitude),
                _ => ResolveCue(cue, "Digital", string.Empty, variant)
            },
            "arcade static" => cue switch
            {
                "app.boot" => ([196.00, 293.66, 440.00], cinematic ? 540 : 480, amplitude * 0.92),
                "ui.focus" => ([622.25], 46, amplitude * 0.72),
                "metadata.apply" => ([587.33, 830.61], 132, amplitude * 0.92),
                "achievement.unlock" => ([466.16, 659.25, 932.33], 220, amplitude),
                "game.launch.handoff" => ([311.13, 466.16, 698.46], cinematic ? 285 : 230, amplitude),
                _ => ResolveCue(cue, "Balanced", string.Empty, variant)
            },
            "tidal glass" => cue switch
            {
                "app.boot" => ([233.08, 349.23, 523.25], cinematic ? 590 : 510, amplitude),
                "ui.focus" => ([523.25], 52, amplitude * 0.7),
                "metadata.apply" => ([698.46, 932.33], 145, amplitude * 0.95),
                "achievement.unlock" => ([493.88, 739.99, 987.77], 235, amplitude),
                "game.launch.handoff" => ([349.23, 523.25, 783.99], cinematic ? 300 : 245, amplitude),
                _ => ResolveCue(cue, "Cinematic", string.Empty, variant)
            },
            "archive ivory" => cue switch
            {
                "app.boot" => ([146.83, 220.00, 329.63], cinematic ? 560 : 480, amplitude * 0.72),
                "app.ready" => ([329.63, 440.00], 118, amplitude * 0.70),
                "menu.open" => ([261.63, 392.00], 82, amplitude * 0.62),
                "tab.switch" => ([293.66], 46, amplitude * 0.56),
                "ui.focus" => ([392.00], 34, amplitude * 0.46),
                "ui.click" => ([246.94], 28, amplitude * 0.48),
                "ui.confirm" => ([329.63, 493.88], 92, amplitude * 0.62),
                "ui.success" => ([293.66, 440.00, 659.25], 130, amplitude * 0.68),
                "metadata.apply" => ([329.63, 493.88], 118, amplitude * 0.62),
                "achievement.unlock" => ([261.63, 392.00, 587.33], 190, amplitude * 0.70),
                "game.launch.handoff" => ([220.00, 329.63, 493.88], cinematic ? 240 : 205, amplitude * 0.68),
                "ui.warning" => ([220.00, 196.00], 128, amplitude * 0.58),
                "ui.error" => ([174.61, 146.83], 150, amplitude * 0.58),
                _ => ([246.94], 36, amplitude * 0.48)
            },
            "midnight observatory" => cue switch
            {
                "app.boot" => ([110.00, 164.81, 246.94, 369.99], cinematic ? 680 : 560, amplitude * 0.72),
                "ui.focus" => ([493.88], 46, amplitude * 0.46),
                "metadata.apply" => ([392.00, 587.33], 142, amplitude * 0.66),
                "achievement.unlock" => ([329.63, 493.88, 739.99], 250, amplitude * 0.76),
                "game.launch.handoff" => ([164.81, 246.94, 369.99], cinematic ? 330 : 260, amplitude * 0.7),
                _ => ResolveCue(cue, "Subtle", string.Empty, variant)
            },
            "verdant glass" => cue switch
            {
                "app.boot" => ([174.61, 261.63, 392.00, 587.33], cinematic ? 590 : 500, amplitude * 0.78),
                "ui.focus" => ([587.33], 40, amplitude * 0.52),
                "metadata.apply" => ([493.88, 739.99], 126, amplitude * 0.72),
                "achievement.unlock" => ([392.00, 587.33, 880.00], 220, amplitude * 0.8),
                "game.launch.handoff" => ([220.00, 329.63, 493.88], cinematic ? 300 : 238, amplitude * 0.74),
                _ => ResolveCue(cue, "Balanced", string.Empty, variant)
            },
            "high contrast" => cue switch
            {
                "app.boot" => ([220.00, 440.00], cinematic ? 420 : 360, amplitude * 0.82),
                "ui.focus" => ([880.00], 30, amplitude * 0.62),
                "ui.click" => ([440.00], 26, amplitude * 0.62),
                "ui.confirm" => ([440.00, 880.00], 76, amplitude * 0.82),
                "ui.warning" => ([330.00, 220.00], 110, amplitude * 0.8),
                "ui.error" => ([220.00, 110.00], 140, amplitude * 0.84),
                _ => ResolveCue(cue, "Balanced", string.Empty, variant)
            },
            "impact noir" => cue switch
            {
                "app.boot" => ([82.41, 123.47, 185.00], cinematic ? 520 : 440, amplitude * 0.9),
                "ui.focus" => ([369.99], 28, amplitude * 0.62),
                "ui.click" => ([146.83, 293.66], 34, amplitude * 0.72),
                "ui.confirm" => ([123.47, 246.94, 493.88], 86, amplitude * 0.88),
                "metadata.apply" => ([185.00, 369.99, 554.37], 116, amplitude * 0.78),
                "achievement.unlock" => ([123.47, 246.94, 493.88, 987.77], 190, amplitude),
                "game.launch.handoff" => ([92.50, 185.00, 369.99], cinematic ? 270 : 210, amplitude * 0.94),
                "ui.warning" => ([164.81, 138.59], 106, amplitude * 0.82),
                "ui.error" => ([110.00, 92.50], 128, amplitude * 0.86),
                _ => ResolveCue(cue, "Digital", string.Empty, variant)
            },
            _ => ResolveCue(cue, "Balanced", string.Empty, variant)
        };
    }

    private static MemoryStream BuildWaveStream(double[] frequencies, int durationMs, double amplitude)
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        var totalSamples = Math.Max(1, sampleRate * durationMs / 1000);
        var dataSize = totalSamples * channels * (bitsPerSample / 8);

        var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);

        for (var i = 0; i < totalSamples; i++)
        {
            var t = i / (double)sampleRate;
            var env = Math.Sin(Math.PI * i / totalSamples);
            var mixed = 0d;
            foreach (var frequency in frequencies)
            {
                mixed += Math.Sin(2 * Math.PI * frequency * t);
            }

            mixed /= Math.Max(1, frequencies.Length);
            var sample = (short)Math.Clamp(mixed * env * amplitude * short.MaxValue, short.MinValue, short.MaxValue);
            writer.Write(sample);
        }

        stream.Position = 0;
        return stream;
    }
}
