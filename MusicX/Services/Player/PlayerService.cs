﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using AsyncAwaitBestPractices;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.DependencyInjection;
using MusicX.Core.Models;
using MusicX.Core.Services;
using MusicX.Helpers;
using MusicX.Models.Enums;
using MusicX.Services.Player.Playlists;
using MusicX.Services.Player.TrackStats;
using MusicX.Shared.Player;
using NLog;
using Wpf.Ui;
using Wpf.Ui.Extensions;

namespace MusicX.Services.Player;

public class PlayerService
{
    public int CurrentIndex;
    private readonly TrackCollection _tracks = new();

    public TrackCollectionBase Tracks => _tracks;
    public PlaylistTrack? CurrentTrack { get; private set; }
    public PlaylistTrack? NextPlayTrack
    {
        get => _nextPlayTrack;
        set
        {
            _nextPlayTrack = value;
            Application.Current.Dispatcher.BeginInvoke(() => NextTrackChanged?.Invoke(this, EventArgs.Empty));
        }
    }
    private DispatcherTimer _positionTimer;
    private readonly MediaPlayer player;

    public bool IsRepeat { get; set; }

    public event EventHandler? NextTrackChanged;
    public event EventHandler? PlayStateChangedEvent;
    public event EventHandler<TimeSpan>? PositionTrackChangedEvent;
    public event EventHandler? TrackChangedEvent;
    public event EventHandler? CurrentPlaylistChanged;

    public event EventHandler<PlayerLoadingEventArgs>? QueueLoadingStateChanged;
    public event EventHandler<PlayerLoadingEventArgs>? TrackLoadingStateChanged;

    private readonly Logger logger;
    private readonly ISnackbarService _snackbarService;
    private readonly IEnumerable<ITrackMediaSource> _mediaSources;
    private readonly IEnumerable<ITrackStatsListener> _statsListeners;
    private readonly ListenTogetherService _listenTogetherService;
    private readonly ConfigService _configService;

    private PlaylistTrack? _nextPlayTrack;
    private CancellationTokenSource? _tokenSource;

    public IPlaylist? CurrentPlaylist { get; set; }

    public PlayerService(Logger logger, ISnackbarService snackbarService,
                         IEnumerable<ITrackMediaSource> mediaSources, IEnumerable<ITrackStatsListener> statsListeners, ListenTogetherService listenTogetherService, ConfigService configService)
    {
        this.logger = logger;
        player = new MediaPlayer();

        InitWindowsComamnds();

        _positionTimer = new DispatcherTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(500);
        _positionTimer.Tick += PositionTimerOnTick;
        _snackbarService = snackbarService;
        _mediaSources = mediaSources;
        _statsListeners = statsListeners;
        this._listenTogetherService = listenTogetherService;
        this._configService = configService;

        SubscribeToListenTogetherEvents();
    }

    public async Task RestoreFromStateAsync(PlayerState state)
    {
        await PlayAsync(state.Playlist, state.Track);
        Seek(state.Position);
    }

    public async void Play()
    {
        if (CurrentTrack is null) return;
        
        await PlayTrackAsync(CurrentTrack);
        
        if (Application.Current.Dispatcher.CheckAccess())
            PlayStateChangedEvent?.Invoke(this, EventArgs.Empty);
        else
            await Application.Current.Dispatcher.InvokeAsync(
                () => PlayStateChangedEvent?.Invoke(this, EventArgs.Empty));
        
            await Task.WhenAll(
                _statsListeners.Select(b => b.TrackPlayStateChangedAsync(CurrentTrack!, player.Position, false)));
    }

    public async Task PlayTrackFromQueueAsync(int index)
    {
        var previousTrack = CurrentTrack!;
        await PlayTrackAsync(Tracks[index]);

        await Task.WhenAll(
                _statsListeners.Select(b => b.TrackChangedAsync(previousTrack, CurrentTrack!, ChangeReason.TrackChange)));
    }
    
    private async Task PlayTrackAsync(PlaylistTrack track)
    {
        try
        {
            if (CurrentTrack == track)
            {
                player.Play();
                return;
            }


            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = new();
            
            player.Pause();

            if (track is null) return;

            CurrentTrack = track;
            CurrentIndex = Tracks.IndexOf(track);
            NextPlayTrack = Tracks.ElementAtOrDefault(CurrentIndex + 1);

            var config = await _configService.GetConfig();

            if (config.IgnoredArtists is null) config.IgnoredArtists = new List<string>();

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                TrackChangedEvent?.Invoke(this, EventArgs.Empty);
                NextTrackChanged?.Invoke(this, EventArgs.Empty);
                PlayStateChangedEvent?.Invoke(this, EventArgs.Empty);
                TrackLoadingStateChanged?.Invoke(this, new(PlayerLoadingState.Started));
            });

            if (CurrentTrack.Data.Url is null) await NextTrack();

            foreach (var ignoreArtist in config.IgnoredArtists)
            {

                if ((CurrentTrack.MainArtists != null && CurrentTrack.MainArtists.Any(a => a.Name == ignoreArtist)) 
                    || (CurrentTrack.FeaturedArtists != null && (CurrentTrack.FeaturedArtists.Any(a => a.Name == ignoreArtist)) ))
                {
                    _snackbarService.Show("Трек пропущен",
                        $"В этом треке был артист из вашего черного списка: {ignoreArtist}. Настроить пропуск треков можно в настройках");
                    await NextTrack();
                }
            }

            if (!(await Task.WhenAll(_mediaSources.Select(b => b.OpenWithMediaPlayerAsync(player, track, _tokenSource.Token)))).Any())
            {
                await NextTrack();
                return;
            }

            player.Play();
            UpdateWindowsData().SafeFireAndForget();

            Application.Current.Dispatcher.BeginInvoke(
                () => TrackLoadingStateChanged?.Invoke(this, new(PlayerLoadingState.Finished)));
        }
        catch(Exception e)
        {
            var properties = new Dictionary<string, string>
            {
#if DEBUG
                { "IsDebug", "True" },
#endif
                {"Version", StaticService.Version }
            };
            Crashes.TrackError(e, properties);
            logger.Error(e);
            _snackbarService.ShowException("Ошибка", "Произошла ошибка при воспроизведении");
        }
       
    }

    public async Task PlayAsync(IPlaylist playlist, PlaylistTrack? firstTrack = null)
    {
        if(_listenTogetherService.PlayerMode == PlayerMode.Listener)
        {
            await _listenTogetherService.LeavePlaySessionAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => PlayAsync(playlist).SafeFireAndForget());
            return;
        }

        if (!playlist.CanLoad)
            throw new InvalidOperationException("Playlist should be loadable for first play");

        try
        {
            if (CurrentPlaylist?.Equals(playlist) is true && Tracks.Count > 0)
            {
                var index = Tracks.IndexOf(firstTrack ?? CurrentTrack ?? Tracks[0]);
                await PlayTrackFromQueueAsync(index);
                return;
            }
            
            CurrentPlaylist = playlist;
            Application.Current.Dispatcher.BeginInvoke(
                () => CurrentPlaylistChanged?.Invoke(this, EventArgs.Empty));

            Task? firstTrackTask = null;
            if (firstTrack is not null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    Tracks.Clear();
                    Tracks.Add(firstTrack);
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Tracks.Clear();
                        Tracks.Add(firstTrack);
                    });
                }
                
                firstTrackTask = PlayTrackAsync(firstTrack);

                    await Task.WhenAll(
                        _statsListeners.Select(b => b.TrackChangedAsync(CurrentTrack, firstTrack, ChangeReason.PlaylistChange)));
            }

            Application.Current.Dispatcher.BeginInvoke(
                () => QueueLoadingStateChanged?.Invoke(this, new(PlayerLoadingState.Started)));

            var loadTask = playlist.LoadAsync().ToArrayAsync().AsTask();

            if (firstTrackTask is null)
                await loadTask;
            else
                await Task.WhenAll(loadTask, firstTrackTask);
            
            Tracks.ReplaceRange(loadTask.Result);
            CurrentIndex = Tracks.IndexOf(CurrentTrack!);
            NextPlayTrack = Tracks.ElementAtOrDefault(CurrentIndex + 1);

            if (firstTrack is null)
            {
                var previousTrack = CurrentTrack;
                await PlayTrackAsync(Tracks[0]);

                await Task.WhenAll(
                        _statsListeners.Select(b => b.TrackChangedAsync(previousTrack, CurrentTrack!, ChangeReason.PlaylistChange)));
            }
        }
        catch (Exception e)
        {
            var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
            Crashes.TrackError(e, properties);
            logger.Error(e);
            _snackbarService.ShowException("Ошибка", "Произошла ошибка при воспроизведении");
        }
        finally
        {
            Application.Current.Dispatcher.BeginInvoke(
                () => QueueLoadingStateChanged?.Invoke(this, new(PlayerLoadingState.Finished)));
        }
    }

    private async Task UpdateWindowsData()
    {
        try
        {
            await Task.Delay(1000);

            var updater = player.SystemMediaTransportControls.DisplayUpdater;


            updater.MusicProperties.Title = CurrentTrack.Title;
            updater.MusicProperties.Artist = CurrentTrack.GetArtistsString();
            updater.MusicProperties.TrackNumber = 1;
            updater.MusicProperties.AlbumArtist = CurrentTrack.MainArtists.First().Name;
            updater.MusicProperties.AlbumTitle = CurrentTrack.AlbumId?.Name ?? string.Empty;
            updater.MusicProperties.AlbumTrackCount = 1;



            if (CurrentTrack.AlbumId?.CoverUrl is not null)
            {
                player.SystemMediaTransportControls.DisplayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(CurrentTrack.AlbumId.CoverUrl));

            }

            player.SystemMediaTransportControls.DisplayUpdater.Update();
        }catch(Exception ex)
        {

            var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
            Crashes.TrackError(ex, properties);

            logger.Error(ex, ex.Message);
        }
           
    }

    public async Task NextTrack()
    {
        if (_listenTogetherService.PlayerMode == PlayerMode.Listener) return;

        try
        {
            logger.Info("Next track");

            async Task LoadMore()
            {
                if (CurrentPlaylist is null) return;
                var array = await CurrentPlaylist!.LoadAsync().ToArrayAsync();
                
                if (Application.Current.Dispatcher.CheckAccess())
                    Tracks.AddRangeSequential(array);
                else
                    await Application.Current.Dispatcher.InvokeAsync(() => Tracks.AddRangeSequential(array));
            }
            
            PlaylistTrack? nextTrack = null;
            if (CurrentIndex + 1 > Tracks.Count - 1)
            {
                if (CurrentPlaylist?.CanLoad == true)
                    await LoadMore();
            }
            else
            {
                nextTrack = Tracks[CurrentIndex + 1];
            }

            if (nextTrack is null)
            {
                Pause();
                return;
            }
            
            CurrentIndex = Tracks.IndexOf(nextTrack);
            
            // its last track and we can load more
            if (CurrentIndex == Tracks.Count - 1 && CurrentPlaylist?.CanLoad == true)
            {
                await LoadMore();
            }

            var previousTrack = CurrentTrack!;

            await PlayTrackAsync(nextTrack);

            await Task.WhenAll(
                    _statsListeners.Select(b => b.TrackChangedAsync(previousTrack, nextTrack, ChangeReason.NextButton)));
        }catch(Exception ex)
        {
            var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
            Crashes.TrackError(ex, properties);
            logger.Error("Error in playerService => NextTrack");
            logger.Error(ex, ex.Message);

            _snackbarService.ShowException("Ошибка", "Произошла ошибка при воспроизведении");

        }
    }

    public double Volume
    {
        get => player.Volume;
        set
        {
            if (player.Volume == value)
                return;

            player.Volume = value;
        }
    }

    public bool IsMuted
    {
        get => player.IsMuted; 
        set => player.IsMuted = value;
    }

      
    public TimeSpan Position => player.PlaybackSession.Position;
    public TimeSpan Duration => player?.NaturalDuration ?? TimeSpan.Zero;
      
    public bool IsPlaying => player.PlaybackSession.PlaybackState is MediaPlaybackState.Playing or MediaPlaybackState.Opening or MediaPlaybackState.Buffering;

    public async void Pause()
    {
        if (!IsPlaying)
            return;
        
        try
        {
            player.Pause();
            
            if (Application.Current.Dispatcher.CheckAccess())
                PlayStateChangedEvent?.Invoke(this, EventArgs.Empty);
            else
                await Application.Current.Dispatcher.InvokeAsync(
                    () => PlayStateChangedEvent?.Invoke(this, EventArgs.Empty));

            await Task.WhenAll(
                _statsListeners.Select(b => b.TrackPlayStateChangedAsync(CurrentTrack!, player.Position, true)));
        }catch (Exception ex)
        {
            logger.Error(ex, ex.Message);
        }
    }

    public void SetVolume(double volume)
    {
        Volume = volume;
    }

    public async void Seek(TimeSpan position, bool sync = false)
    {
        try
        {
            if (_listenTogetherService.PlayerMode == PlayerMode.Listener && !sync) return;
            if (position == TimeSpan.Zero)
                position = TimeSpan.FromSeconds(0.5);
            
            player.PlaybackSession.Position = position;

           await Task.WhenAll(
                    _statsListeners.Select(b => b.TrackPlayStateChangedAsync(CurrentTrack!, position, !IsPlaying)));
        }
        catch (Exception e)
        {
            logger.Error(e, e.Message);
        }
    }
    
    public async Task PreviousTrack()
    {
        if (_listenTogetherService.PlayerMode == PlayerMode.Listener) return;

        try
        {
            logger.Info("Go to prev track");
            if (Position.TotalSeconds > 10) Seek(TimeSpan.Zero);
            else
            {

                var index = CurrentIndex - 1;
                if (index < 0) index = Tracks.Count - 1;

                var previousTrack = Tracks[index];
                var prevCurrentTrack = CurrentTrack!;
                
                await PlayTrackAsync(previousTrack);
                
                await Task.WhenAll(
                        _statsListeners.Select(b => b.TrackChangedAsync(prevCurrentTrack, previousTrack!, ChangeReason.PrevButton)));
            }
        }
        catch (Exception e)
        {
            var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
            Crashes.TrackError(e, properties);

            logger.Error("Error in playerService => PreviousTrack");
            logger.Error(e, e.Message);

            _snackbarService.ShowException("Ошибка", "Произошла ошибка при воспроизведении");
        }

    }

    private void MediaPlayerOnCurrentStateChanged(MediaPlaybackSession sender, object args)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (sender.PlaybackState == MediaPlaybackState.Playing)
                    _positionTimer.Start();
                else
                    _positionTimer.Stop();

                PlayStateChangedEvent?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception e)
        {

            var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
            Crashes.TrackError(e, properties);

            logger.Error(e, e.Message);

            _snackbarService.ShowException("Ошибка", "Произошла ошибка при воспроизведении");

        }

    }

    private async void MediaPlayerOnMediaEnded(MediaPlayer sender, object args)
    {
        if (_listenTogetherService.PlayerMode == PlayerMode.Listener) return;
        try
        {
            if(IsRepeat)
            {
                this.player.Pause();
                player.Position = TimeSpan.Zero;
                this.player.Play();
                return;
            }
            await NextTrack();
        }
        catch (Exception e)
        {

            var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
            Crashes.TrackError(e, properties);

            _snackbarService.ShowException("Ошибка", "Произошла ошибка при воспроизведении");

            logger.Error(e, e.Message);
        }
    }


    public void SetShuffle(bool shuffle)
    {
        try
        {
            logger.Info($"SET SHUFFLE: {shuffle}");
            
            if (shuffle)
                _tracks.ShuffleTracks(CurrentIndex);
            else
                _tracks.RevertShuffle();
        }
        catch(Exception ex)
        {
            var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
            Crashes.TrackError(ex, properties);

            _snackbarService.ShowException("Ошибка", "Произошла ошибка при перемешивании");

            logger.Error(ex, ex.Message);
        }
        
    }

    public void SetRepeat(bool repeat)
    {
        logger.Info($"SET REPEAT: {repeat}");

        this.IsRepeat = repeat;
    }


    private async void MediaPlayerOnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        if (args.Error == MediaPlayerError.SourceNotSupported)
        {
            logger.Error("Error SourceNotSupported player");

            _snackbarService.ShowException("Ошибка", "Произошла ошибкба SourceNotSupported");

            if(CurrentTrack is not null && CurrentTrack.Data.Url.EndsWith(".mp3"))
            {
                try
                {
                    //Какая же это все грязь, пиздец.

                    var vkService = StaticService.Container.GetRequiredService<VkService>();

                    var boomService = StaticService.Container.GetRequiredService<BoomService>();
                    var configService = StaticService.Container.GetRequiredService<ConfigService>();

                    var config = await configService.GetConfig();
                    var boomVkToken = await vkService.GetBoomToken();

                    var boomToken = await boomService.AuthByTokenAsync(boomVkToken.Token, boomVkToken.Uuid);

                    config.BoomToken = boomToken.AccessToken;
                    config.BoomTokenTtl = DateTimeOffset.Now + TimeSpan.FromSeconds(boomToken.ExpiresIn);
                    config.BoomUuid = boomVkToken.Uuid;

                    await configService.SetConfig(config);

                    boomService.SetToken(boomToken.AccessToken);

                    await PlayTrackAsync(CurrentTrack);

                }
                catch (Exception ex)
                {
                    var properties = new Dictionary<string, string>
                {
#if DEBUG
                    { "IsDebug", "True" },
#endif
                    {"Version", StaticService.Version }
                };
                    Crashes.TrackError(ex, properties);

                }

                return;
            }

            if (CurrentTrack is not null)
                //audio source url may expire
                await PlayTrackAsync(CurrentTrack);
        }
        else if (args.Error == MediaPlayerError.NetworkError)
        {
            _snackbarService.ShowException("Ошибка", "Мы не смогли воспроизвести трек из-за проблем с сетью");

            logger.Error("Network Error player");
        }
    }

    private int positionTimerListenTogetherCouter = 0;
    private async void PositionTimerOnTick(object sender, object o)
    {
        Application.Current.Dispatcher.Invoke((() =>
        {
            PositionTrackChangedEvent?.Invoke(this, Position);
        }));

        if (_listenTogetherService.PlayerMode == PlayerMode.Owner)
        {
            if (positionTimerListenTogetherCouter == 3)
            {
                positionTimerListenTogetherCouter = 0;
            }

            positionTimerListenTogetherCouter++;

            await _listenTogetherService.ChangePlayStateAsync(this.Position, !IsPlaying);
        }
    }
    public async void RemoveFromQueue(PlaylistTrack audio)
    {
        if (audio == CurrentTrack)
        {
            if (CurrentIndex + 1 < Tracks.Count)
                await NextTrack();
            else
                Pause();
        }

        if (!Tracks.Remove(audio))
            return;
            
        CurrentIndex = Tracks.IndexOf(CurrentTrack!);
            
        if (audio == NextPlayTrack)
            NextPlayTrack = Tracks.ElementAtOrDefault(CurrentIndex + 1);
    }

    public async void InsertToQueue(PlaylistTrack audio, bool afterCurrent)
    {
        if (Tracks.Count == 0 && !IsPlaying)
        {
            Tracks.Add(audio);
            // so we dont had to deal with deadlocks if insertion was triggered with dispatcher context
            await PlayTrackAsync(audio).ConfigureAwait(false);
            return;
        }
            
        if (afterCurrent)
        {
            Tracks.Insert(CurrentIndex + 1, audio);
            NextPlayTrack = audio;
        }
        else
        {
            // if current is last track in the queue
            if (CurrentIndex == Tracks.Count - 1)
                NextPlayTrack = audio;
                
            Tracks.Add(audio);
        }
            
        await Application.Current.Dispatcher.InvokeAsync(() => NextTrackChanged?.Invoke(this, EventArgs.Empty));
    }

    private void InitWindowsComamnds()
    {
        try
        {

            player.AudioCategory = MediaPlayerAudioCategory.Media;
            player.Play();

            player.PlaybackSession.PlaybackStateChanged += MediaPlayerOnCurrentStateChanged;
            player.MediaEnded += MediaPlayerOnMediaEnded;
            player.MediaFailed += MediaPlayerOnMediaFailed;

            player.CommandManager.NextBehavior.EnablingRule = MediaCommandEnablingRule.Always;
            player.CommandManager.PreviousBehavior.EnablingRule = MediaCommandEnablingRule.Always;

            player.SystemMediaTransportControls.DisplayUpdater.Type = MediaPlaybackType.Music;
            player.CommandManager.NextReceived += async (c, e) => await NextTrack();
            player.CommandManager.PreviousReceived += async (c, e) => await PreviousTrack();
            player.CommandManager.PlayReceived += (c, e) => Play();
            player.CommandManager.PauseReceived += (c, e) => Pause();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.Message);
        }
    }

    private void SubscribeToListenTogetherEvents()
    {
        _listenTogetherService.PlayStateChanged += ListenTogetherPlayStateChanged;
        _listenTogetherService.TrackChanged += ListenTogetherTrackChanged;
        _listenTogetherService.ConnectedToSession += ListenTogetherConnectedToSession;
        _listenTogetherService.SessionOwnerStoped += SessionStoped;
    }

    private async Task SessionStoped()
    {
        Pause();
    }

    private async Task ListenTogetherConnectedToSession(PlaylistTrack playlistTrack)
    {
        await this.PlayTrackAsync(playlistTrack);
    }

    private async Task ListenTogetherTrackChanged(PlaylistTrack playlistTrack)
    {
        await this.PlayTrackAsync(playlistTrack);
    }

    private async Task ListenTogetherPlayStateChanged(TimeSpan position, bool pause)
    {
       if(IsPlaying != !pause)
       {
            if (pause) Pause();
            else
            {
                Play();
            }
       }

        if((this.Position.TotalSeconds - position.TotalSeconds) > 2 || (this.Position.TotalSeconds - position.TotalSeconds) < - 2)
        {
            Seek(position, true);
        }
    }

    private class TrackCollection : TrackCollectionBase
    {
        private List<PlaylistTrack>? _originalTracks;
        private List<int>? _shuffledIndexes;
        
        public override bool Shuffle { get; protected set; } 
        
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            
            if (Shuffle && e.Action == NotifyCollectionChangedAction.Reset)
            {
                _originalTracks = null;
                _shuffledIndexes = null;
                Shuffle = false;
            }
        }

        public void ShuffleTracks(int? startingIndex)
        {
            if (Shuffle)
                throw new InvalidOperationException("Already shuffled");
            
            CheckReentrancy();
            
            var shuffledIndexes = new int[Items.Count];
            
            if (startingIndex is > 0)
            {
                (Items[startingIndex.Value], Items[0]) = (Items[0], Items[startingIndex.Value]);
                shuffledIndexes[0] = 0;
            }
            
            _originalTracks = Items.ToList();
            
            for (var i = Items.Count - 1; i > 0; i--)
            {
                var k = Random.Shared.Next(1, i + 1);
                (Items[k], Items[i]) = (Items[i], Items[k]);
                shuffledIndexes[k] = i;
            }
            
            _shuffledIndexes = new(shuffledIndexes);

            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Reset);
            Shuffle = true;
        }

        public void RevertShuffle()
        {
            if (!Shuffle)
                throw new InvalidOperationException("Not shuffled");
            
            Shuffle = false;
            ReplaceRange(_originalTracks!);
        }

        public override void AddRangeSequential(IEnumerable<PlaylistTrack> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            CheckReentrancy();

            var enumerable = collection as IList<PlaylistTrack> ?? collection.ToArray();

            if (Shuffle)
            {
                _originalTracks!.AddRange(enumerable);
                
                var shuffledIndexes = new int[enumerable.Count];
                
                for (var i = enumerable.Count - 1; i >= 0; i--)
                {
                    var k = Random.Shared.Next(i + 1);
                    (enumerable[k], enumerable[i]) = (enumerable[i], enumerable[k]);
                    shuffledIndexes[k] = _shuffledIndexes!.Count + i;
                }
                
                _shuffledIndexes!.AddRange(shuffledIndexes);
            }
            
            base.AddRangeSequential(enumerable);
        }

        protected override void InsertItem(int index, PlaylistTrack item)
        {
            base.InsertItem(index, item);
            if (!Shuffle) return;
            
            _originalTracks!.Add(item);
            _shuffledIndexes!.Add(index);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            if (!Shuffle) return;
            
            _originalTracks!.RemoveAt(_shuffledIndexes![index]);
            _shuffledIndexes.RemoveAt(index);
        }
    }
}

public abstract class TrackCollectionBase : ObservableRangeCollection<PlaylistTrack>
{
    public abstract bool Shuffle { get; protected set; }
}