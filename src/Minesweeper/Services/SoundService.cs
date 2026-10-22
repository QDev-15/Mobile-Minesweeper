using Plugin.Maui.Audio;

namespace Minesweeper.Services;

/// <summary>Short gameplay sound effects (owner's direction, 2026-10-20: "Thêm âm thanh - rung cho game khi
/// chơi"). Every clip is a synthesised WAV generated for this app specifically (no third-party asset, so no
/// licensing question) bundled under Resources/Raw -- see MauiAsset in Minesweeper.csproj. Fire-and-forget
/// by design (Play* methods return void): a sound effect must never make the UI thread wait on it, the same
/// reasoning as GamePage's own cell-pop animation.</summary>
public interface ISoundService
{
    void PlayReveal();
    void PlayFlag();
    void PlayExplosion();
    void PlayWin();
    void PlayHint();
}

public sealed class SoundService : ISoundService
{
    private readonly IAudioManager _audioManager;
    private readonly Dictionary<string, byte[]> _cache = [];

    public SoundService(IAudioManager audioManager) => _audioManager = audioManager;

    public void PlayReveal() => Play("reveal.wav");
    public void PlayFlag() => Play("flag.wav");
    public void PlayExplosion() => Play("explosion.wav");
    public void PlayWin() => Play("win.wav");
    public void PlayHint() => Play("hint.wav");

    private void Play(string fileName)
    {
        if (!AppPreferences.SoundEnabled) return;
        _ = PlayAsync(fileName);
    }

    private async Task PlayAsync(string fileName)
    {
        IAudioPlayer? player = null;
        try
        {
            byte[] bytes = await GetBytesAsync(fileName);
            player = _audioManager.CreatePlayer(new MemoryStream(bytes));
            player.PlaybackEnded += (_, _) => player.Dispose();
            player.Play();
        }
        catch
        {
            // Best-effort only: a sound effect is cosmetic, so a platform audio-session hiccup (or, in theory,
            // a missing asset) must never surface to the player as a crash or a broken tap/flag/reveal. Still
            // dispose a player that was created but never reached PlaybackEnded (Play() itself threw) --
            // otherwise that one native player leaks every time this happens (found in review, 2026-10-20).
            player?.Dispose();
        }
    }

    private async Task<byte[]> GetBytesAsync(string fileName)
    {
        if (_cache.TryGetValue(fileName, out byte[]? cached)) return cached;
        using Stream stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        byte[] bytes = ms.ToArray();
        _cache[fileName] = bytes;
        return bytes;
    }
}
