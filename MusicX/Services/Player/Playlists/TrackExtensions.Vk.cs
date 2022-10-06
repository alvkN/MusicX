﻿using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MusicX.Core.Models;
using MusicX.Core.Services;
using MusicX.ViewModels;
using VkNet.Abstractions;

namespace MusicX.Services.Player.Playlists;

public static partial class TrackExtensions
{
    public static PlaylistTrack ToTrack(this Audio audio) => ToTrack(audio, null);
    
    public static PlaylistTrack ToTrack(this Audio audio, Playlist? playlist)
    {
        TrackArtist[] mainArtists;
        if (audio.MainArtists is null || !audio.MainArtists.Any())
            mainArtists = new[] { new TrackArtist(audio.Artist, null) };
        else
            mainArtists = audio.MainArtists.Select(ToTrackArtist).ToArray();

        var isLiked = audio.OwnerId == StaticService.Container.GetRequiredService<IVkApi>().UserId!.Value;

        return new(audio.Title, audio.Subtitle, audio.Album?.ToAlbumId(), mainArtists,
                   audio.FeaturedArtists?.Select(ToTrackArtist).ToArray() ?? Array.Empty<TrackArtist>(),
                   new VkTrackData(audio.Url, isLiked, audio.IsExplicit, TimeSpan.FromSeconds(audio.Duration), new(
                                       audio.Id,
                                       audio.OwnerId, audio.AccessKey), audio.TrackCode, audio.ParentBlockId,
                                   playlist is null ? null : new(playlist.Id, playlist.OwnerId, playlist.AccessKey)));
    }

    public static TrackArtist ToTrackArtist(this MainArtist artist)
    {
        return new(artist.Name, new(artist.Id, ArtistIdType.Vk));
    }

    public static VkAlbumId ToAlbumId(this Album album)
    {
        return new(album.Id, album.OwnerId, album.AccessKey, album.Title, album.Cover);
    }
}