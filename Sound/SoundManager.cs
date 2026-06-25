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

    public SoundManager()
    {
        _removeSoundNames = DiscoverNumberedVariants("remove");
    }

    public static bool IsBackgroundMusicAvailable() => BackgroundMusicAvailable.Value;

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

    public void PreviewEffect(string baseName, int volumePercent)
    {
        PlayEffect(baseName, volumePercent);
    }

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

    public void PlayGameOver()
    {
        if (!_effectsEnabled)
        {
            return;
        }

        PlayEffect("game-end", _effectsVolume);
    }

    public void Shutdown()
    {
        StopBackgroundMusic();
    }

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

    private void StopBackgroundMusic()
    {
        lock (_backgroundLock)
        {
            StopBackgroundMusicInternal();
        }
    }

    private void StopBackgroundMusicInternal()
    {
        _backgroundPlaybackEpoch++;
        if (_backgroundPlayer is not null)
        {
            _backgroundPlayer.Dispose();
            _backgroundPlayer = null;
        }
    }

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

    private static IReadOnlyList<string> DiscoverBackgroundTracks() =>
        DiscoverNumberedVariants("background");

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

    private static IEnumerable<string> GetSoundSearchRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "sounds");

        string? packageRoot = TryGetPackageSoundRoot();
        if (packageRoot is not null)
        {
            yield return packageRoot;
        }
    }

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

    private static int ClampVolume(int volume) =>
        Math.Max(0, Math.Min(100, volume));

    private static double VolumeScale(int volumePercent) =>
        ClampVolume(volumePercent) / 100.0;
}
