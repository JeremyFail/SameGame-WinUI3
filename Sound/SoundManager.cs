using System.Collections.Concurrent;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace SameGame.Sound;

/// <summary>
/// Plays sound effects and optional background music from MP3 assets under Assets/sounds/.
/// </summary>
public sealed class SoundManager
{
    private const int MaxSoundVariants = 99;
    private static readonly Lazy<IReadOnlyList<string>> BackgroundTrackNames = new(DiscoverBackgroundTracks);
    private static readonly Lazy<bool> BackgroundMusicAvailable = new(() => BackgroundTrackNames.Value.Count > 0);

    private readonly List<string> _removeSoundNames;
    private readonly List<MediaPlayer> _activeEffectPlayers = [];
    private readonly object _effectPlayersLock = new();
    private readonly Random _random = new();
    private readonly object _backgroundLock = new();

    private MediaPlayer? _backgroundPlayer;
    private int _backgroundPlaybackEpoch;
    private string? _lastBackgroundTrack;

    private bool _effectsEnabled = true;
    private bool _backgroundMusicEnabled;
    private int _effectsVolume = 100;
    private int _backgroundMusicVolume = 50;

    /// <summary>
    /// Initializes a new sound manager and discovers available remove-tile effect variants.
    /// </summary>
    public SoundManager()
    {
        _removeSoundNames = DiscoverNumberedVariants("remove");
    }

    /// <summary>
    /// Determines whether at least one background music track is available on disk.
    /// </summary>
    /// <returns><see langword="true"/> when background music assets exist; otherwise <see langword="false"/>.</returns>
    public static bool IsBackgroundMusicAvailable() => BackgroundMusicAvailable.Value;

    /// <summary>
    /// Applies sound-effect and background-music enable flags and volume levels.
    /// </summary>
    /// <param name="effectsEnabled">Whether sound effects should play.</param>
    /// <param name="effectsVolume">Sound-effects volume from 0 to 100.</param>
    /// <param name="backgroundMusicEnabled">Whether background music should play when assets exist.</param>
    /// <param name="backgroundMusicVolume">Background-music volume from 0 to 100.</param>
    public void Configure(
        bool effectsEnabled,
        int effectsVolume,
        bool backgroundMusicEnabled,
        int backgroundMusicVolume)
    {
        _effectsEnabled = effectsEnabled;
        _effectsVolume = ClampVolume(effectsVolume);
        _backgroundMusicEnabled = backgroundMusicEnabled && IsBackgroundMusicAvailable();
        _backgroundMusicVolume = ClampVolume(backgroundMusicVolume);
        UpdateBackgroundMusic();
    }

    /// <summary>
    /// Updates the volume of the currently playing background music track.
    /// </summary>
    /// <param name="volumePercent">Background-music volume from 0 to 100.</param>
    public void SetBackgroundMusicVolume(int volumePercent)
    {
        _backgroundMusicVolume = ClampVolume(volumePercent);
        lock (_backgroundLock)
        {
            if (_backgroundPlayer is not null)
            {
                _backgroundPlayer.Volume = VolumeScale(_backgroundMusicVolume);
            }
        }
    }

    /// <summary>
    /// Plays a one-shot preview of a named sound effect at the given volume.
    /// </summary>
    /// <param name="baseName">The sound asset base name without extension.</param>
    /// <param name="volumePercent">Preview volume from 0 to 100.</param>
    public void PreviewEffect(string baseName, int volumePercent)
    {
        PlayEffect(baseName, volumePercent);
    }

    /// <summary>
    /// Plays a random remove-tile sound effect when effects are enabled.
    /// </summary>
    public void PlayRemove()
    {
        if (!_effectsEnabled)
        {
            return;
        }

        if (_removeSoundNames.Count == 0)
        {
            return;
        }

        string name = _removeSoundNames[_random.Next(_removeSoundNames.Count)];
        PlayEffect(name, _effectsVolume);
    }

    /// <summary>
    /// Plays the game-over sound effect when effects are enabled.
    /// </summary>
    public void PlayGameOver()
    {
        if (!_effectsEnabled)
        {
            return;
        }

        PlayEffect("game-end", _effectsVolume);
    }

    /// <summary>
    /// Stops background music and releases playback resources.
    /// </summary>
    public void Shutdown()
    {
        StopBackgroundMusic();
    }

    /// <summary>
    /// Creates, tracks, and plays a one-shot sound effect from a named asset.
    /// </summary>
    /// <param name="baseName">The sound asset base name without extension.</param>
    /// <param name="volumePercent">Playback volume from 0 to 100.</param>
    private void PlayEffect(string baseName, int volumePercent)
    {
        if (!TryCreateMediaSource(baseName, out var source))
        {
            return;
        }

        var player = new MediaPlayer
        {
            Volume = VolumeScale(volumePercent),
            Source = source
        };
        lock (_effectPlayersLock)
        {
            _activeEffectPlayers.Add(player);
        }

        void Finish()
        {
            player.MediaEnded -= OnEnded;
            player.MediaFailed -= OnFailed;
            lock (_effectPlayersLock)
            {
                _activeEffectPlayers.Remove(player);
            }

            player.Dispose();
        }

        void OnEnded(MediaPlayer sender, object args) => Finish();
        void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) => Finish();
        player.MediaEnded += OnEnded;
        player.MediaFailed += OnFailed;
        player.Play();
    }

    /// <summary>
    /// Starts, resumes, or stops background music based on the current enabled state.
    /// </summary>
    private void UpdateBackgroundMusic()
    {
        if (!_backgroundMusicEnabled)
        {
            StopBackgroundMusic();
            return;
        }

        lock (_backgroundLock)
        {
            if (_backgroundPlayer is not null)
            {
                _backgroundPlayer.Volume = VolumeScale(_backgroundMusicVolume);
                if (_backgroundPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                {
                    return;
                }
            }
        }

        StartBackgroundTrack(PickBackgroundTrackName());
    }

    /// <summary>
    /// Stops any current background track and begins playback of the specified track.
    /// </summary>
    /// <param name="trackName">The background track asset base name without extension.</param>
    private void StartBackgroundTrack(string trackName)
    {
        lock (_backgroundLock)
        {
            if (!_backgroundMusicEnabled)
            {
                return;
            }

            StopBackgroundMusicInternal();
            int epoch = ++_backgroundPlaybackEpoch;
            _lastBackgroundTrack = trackName;

            if (!TryCreateMediaSource(trackName, out var source))
            {
                return;
            }

            var player = new MediaPlayer
            {
                Volume = VolumeScale(_backgroundMusicVolume),
                Source = source,
                IsLoopingEnabled = false
            };
            player.MediaEnded += (_, _) => OnBackgroundTrackEnded(epoch);
            player.MediaFailed += (_, _) => OnBackgroundTrackEnded(epoch);
            _backgroundPlayer = player;
            player.Play();
        }
    }

    /// <summary>
    /// Handles completion or failure of a background track and optionally starts the next track.
    /// </summary>
    /// <param name="epoch">The playback epoch that must still be current to continue the playlist.</param>
    private void OnBackgroundTrackEnded(int epoch)
    {
        bool playNext;
        lock (_backgroundLock)
        {
            if (_backgroundPlayer is not null)
            {
                _backgroundPlayer.Dispose();
                _backgroundPlayer = null;
            }

            playNext = epoch == _backgroundPlaybackEpoch && _backgroundMusicEnabled;
        }

        if (playNext)
        {
            StartBackgroundTrack(PickBackgroundTrackName());
        }
    }

    /// <summary>
    /// Stops background music playback under the background lock.
    /// </summary>
    private void StopBackgroundMusic()
    {
        lock (_backgroundLock)
        {
            StopBackgroundMusicInternal();
        }
    }

    /// <summary>
    /// Disposes the active background player and invalidates in-flight playback callbacks.
    /// </summary>
    private void StopBackgroundMusicInternal()
    {
        _backgroundPlaybackEpoch++;
        if (_backgroundPlayer is not null)
        {
            _backgroundPlayer.Dispose();
            _backgroundPlayer = null;
        }
    }

    /// <summary>
    /// Selects a background track name, avoiding immediate repetition when multiple tracks exist.
    /// </summary>
    /// <returns>The base name of the background track to play.</returns>
    private string PickBackgroundTrackName()
    {
        var tracks = BackgroundTrackNames.Value;
        if (tracks.Count == 0)
        {
            return "background1";
        }

        if (tracks.Count == 1)
        {
            return tracks[0];
        }

        string name;
        do
        {
            name = tracks[_random.Next(tracks.Count)];
        }
        while (name == _lastBackgroundTrack);

        return name;
    }

    /// <summary>
    /// Resolves a media source for a sound asset from disk search paths or packaged content.
    /// </summary>
    /// <param name="baseName">The sound asset base name without extension.</param>
    /// <param name="source">When this method returns, contains the resolved source if found.</param>
    /// <returns><see langword="true"/> when a media source was created; otherwise <see langword="false"/>.</returns>
    private static bool TryCreateMediaSource(string baseName, out MediaSource? source)
    {
        source = null;
        foreach (string root in GetSoundSearchRoots())
        {
            string path = Path.Combine(root, baseName + ".mp3");
            if (!File.Exists(path))
            {
                continue;
            }

            source = MediaSource.CreateFromUri(new Uri(path));
            return true;
        }

        try
        {
            source = MediaSource.CreateFromUri(new Uri($"ms-appx:///Assets/sounds/{baseName}.mp3"));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Collects numbered sound variant base names that exist for a given prefix.
    /// </summary>
    /// <param name="prefix">The shared prefix before the variant number (for example, <c>remove</c>).</param>
    /// <returns>Ordered list of existing variant base names.</returns>
    private static List<string> DiscoverNumberedVariants(string prefix)
    {
        var names = new List<string>();
        for (int i = 1; i <= MaxSoundVariants; i++)
        {
            string name = $"{prefix}{i}";
            if (SoundFileExists(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Discovers all available numbered background music tracks.
    /// </summary>
    /// <returns>Read-only list of background track base names.</returns>
    private static IReadOnlyList<string> DiscoverBackgroundTracks() =>
        DiscoverNumberedVariants("background");

    /// <summary>
    /// Determines whether an MP3 file exists for the given sound base name.
    /// </summary>
    /// <param name="baseName">The sound asset base name without extension.</param>
    /// <returns><see langword="true"/> when the file exists in a search root; otherwise <see langword="false"/>.</returns>
    private static bool SoundFileExists(string baseName)
    {
        string fileName = baseName + ".mp3";
        foreach (string root in GetSoundSearchRoots())
        {
            if (File.Exists(Path.Combine(root, fileName)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Yields filesystem directories searched for loose sound assets.
    /// </summary>
    /// <returns>Candidate sound root directories in search order.</returns>
    private static IEnumerable<string> GetSoundSearchRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "sounds");

        string? packageRoot = TryGetPackageSoundRoot();
        if (packageRoot is not null)
        {
            yield return packageRoot;
        }
    }

    /// <summary>
    /// Attempts to resolve the packaged application's Assets/sounds directory.
    /// </summary>
    /// <returns>The absolute sounds directory path, or <see langword="null"/> when unavailable.</returns>
    private static string? TryGetPackageSoundRoot()
    {
        try
        {
            return Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                "Assets",
                "sounds");
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Clamps a volume percentage to the inclusive range 0 through 100.
    /// </summary>
    /// <param name="volume">The requested volume percentage.</param>
    /// <returns>The clamped volume percentage.</returns>
    private static int ClampVolume(int volume) =>
        Math.Max(0, Math.Min(100, volume));

    /// <summary>
    /// Converts a volume percentage to a media player volume scale from 0.0 to 1.0.
    /// </summary>
    /// <param name="volumePercent">The volume percentage from 0 to 100.</param>
    /// <returns>The scaled volume suitable for <see cref="MediaPlayer.Volume"/>.</returns>
    private static double VolumeScale(int volumePercent) =>
        ClampVolume(volumePercent) / 100.0;
}
