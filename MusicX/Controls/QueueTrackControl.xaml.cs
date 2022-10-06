﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;
using MusicX.Services;
using MusicX.Services.Player;
using MusicX.Services.Player.Playlists;

namespace MusicX.Controls;

public partial class QueueTrackControl : UserControl
{
    private readonly PlayerService _player;

    public static readonly DependencyProperty IsCurrentlyPlayingProperty = DependencyProperty.Register(
        nameof(IsCurrentlyPlaying), typeof(bool), typeof(QueueTrackControl), new(PropertyChangedCallback));

    private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not QueueTrackControl control) return;

        var sb = (Storyboard)control.FindResource(control._player.CurrentTrack == (PlaylistTrack?)control.DataContext ? "PlayBorderFadeIn" : "PlayBorderFadeOut");
        sb.Begin();
    }

    public bool IsCurrentlyPlaying
    {
        get => (bool)GetValue(IsCurrentlyPlayingProperty);
        set => SetValue(IsCurrentlyPlayingProperty, value);
    }

    public QueueTrackControl()
    {
        InitializeComponent();
        _player = StaticService.Container.GetRequiredService<PlayerService>();
        _player.PlayStateChangedEvent += PlayerOnPlayStateChangedEvent;
    }

    private void PlayerOnPlayStateChangedEvent(object? sender, EventArgs e)
    {
        if (DataContext is not PlaylistTrack track) return;

        IsCurrentlyPlaying = _player.CurrentTrack == track;
        IconPlay.Symbol = _player.IsPlaying && IsCurrentlyPlaying ? Wpf.Ui.Common.SymbolRegular.Pause24 : Wpf.Ui.Common.SymbolRegular.Play24;
    }

    private void Track_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PlaylistTrack track) return;
        
        if (IsCurrentlyPlaying)
        {
            if (_player.IsPlaying)
                _player.Pause();
            else
                _player.Play();
        }
        else
        {
            _player.PlayTrackFromQueueAsync(_player.Tracks.IndexOf(track)).SafeFireAndForget();
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (IsCurrentlyPlaying) return;
        
        var sb = (Storyboard)FindResource("PlayBorderFadeIn");
        sb.Begin();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (IsCurrentlyPlaying) return;
        
        var sb = (Storyboard)FindResource("PlayBorderFadeOut");
        sb.Begin();
    }

    private void QueueTrackControl_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        PlayerOnPlayStateChangedEvent(_player, EventArgs.Empty);
    }
}