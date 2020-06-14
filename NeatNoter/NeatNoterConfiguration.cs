﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
using NeatNoter.Models;
using Newtonsoft.Json;

namespace NeatNoter
{
    public class NeatNoterConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool IncludeNoteBodiesInSearch { get; set; }
        public float PenThickness { get; set; }
        public Vector3 PenColor { get; set; }

        public List<Note> Notes { get; set; } // TODO: Add backup functionality
        public List<Category> Categories { get; set; }

        public NeatNoterConfiguration()
        {
            Notes = new List<Note>();
            Categories = new List<Category>();

            PenThickness = 2.0f;
            PenColor = new Vector3(1.0f, 1.0f, 1.0f);
        }

        [JsonIgnore]
        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            Notes.InitializeAll(this.pluginInterface);
            Categories.InitializeAll(this.pluginInterface);
        }

        public void Save()
        {
            foreach (var category in Categories)
            {
                category.CompressBody();
            }
            foreach (var note in Notes)
            {
                note.CompressBody();
            }

            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
