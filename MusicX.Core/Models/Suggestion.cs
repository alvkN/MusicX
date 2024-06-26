﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicX.Core.Models.Abstractions;

namespace MusicX.Core.Models
{
    public class Suggestion : IBlockEntity<string>
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("subtitle")]
        public string Subtitle { get; set; }

        [JsonProperty("context")]
        public string Context { get; set; }
    }
}
