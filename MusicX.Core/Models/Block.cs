﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicX.Core.Models
{
    public class Block
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("data_type")]
        public string DataType { get; set; }

        [JsonProperty("layout")]
        public Layout Layout { get; set; }

        [JsonProperty("catalog_banner_ids")]
        public List<int> CatalogBannerIds { get; set; } = new List<int>();

        [JsonProperty("links_ids")]
        public List<string> LinksIds { get; set; } = new List<string>();

        [JsonProperty("buttons")]
        public List<Button> Buttons { get; set; }

        [JsonProperty("next_from")]
        public string NextFrom { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("listen_events")]
        public List<string> ListenEvents { get; set; }

        [JsonProperty("playlists_ids")]
        public List<string> PlaylistsIds { get; set; } = new List<string>();

        [JsonProperty("suggestions_ids")]
        public List<string> SuggestionsIds { get; set; } = new List<string>();

        [JsonProperty("artists_ids")]
        public List<string> ArtistsIds { get; set; } = new List<string>();

        [JsonProperty("badge")]
        public Badge Badge { get; set; }

        [JsonProperty("audios_ids")]
        public List<string> AudiosIds { get; set; } = new List<string>();

        [JsonProperty("curators_ids")]
        public List<long> CuratorsIds { get; set; } = new List<long>();

        [JsonProperty("group_ids")]
        public List<long> GroupIds { get; set; } = new List<long>();

        [JsonProperty("text_ids")]
        public List<string> TextIds { get; set; } = new List<string>();


        public List<Curator> Curators { get; set; } = new List<Curator>();
        public List<Text> Texts { get; set; } = new List<Text>();
        public List<Audio> Audios { get; set; } = new List<Audio>();
        public List<Playlist> Playlists { get; set; } = new List<Playlist>();
        public List<CatalogBanner> Banners { get; set; } = new List<CatalogBanner>();
        public List<Link> Links { get; set; } = new List<Link>();
        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();
        public List<Artist> Artists { get; set; } = new List<Artist>();
        public List<Group> Groups { get; set; } = new List<Group>();

    }
}
