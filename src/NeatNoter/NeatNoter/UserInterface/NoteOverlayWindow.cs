using System;
using CheapLoc;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;

namespace NeatNoter.NeatNoter.UserInterface
{
    public class NoteOverlayWindow : PluginWindow
    {
        /// <summary>
        /// Currently selected note.
        /// </summary>
        public Note? CurrentNote;

        private static readonly uint TextColor = ImGui.GetColorU32(ImGuiCol.Text);

        /// <summary>
        /// Initializes a new instance of the <see cref="NoteOverlayWindow"/> class.
        /// </summary>
        /// <param name="plugin">NeatNoter plugin.</param>
        public NoteOverlayWindow(NeatNoterPlugin plugin)
            : base(plugin, "NeatNoterOverlay")
        {
            this.plugin = plugin;
            this.Size = new Vector2(400, 600) * ImGui.GetIO().FontGlobalScale;
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar;
        }

        /// <inheritdoc />
        public override void OnOpen()
        {
            this.plugin.Configuration.IsVisible = true;
            this.plugin.SaveConfig();
        }

        /// <inheritdoc />
        public override void OnClose()
        {
            this.plugin.Configuration.IsVisible = true;
        }

        /// <inheritdoc/>
        public override void Draw()
        {
            try
            {
                if (this.CurrentNote != null)
                {
                    ImGui.Text(this.CurrentNote.Name);
                    ImGui.Text(this.CurrentNote.Body);

                    if (ImGui.BeginPopupContextItem("###NeatNoter_" + this.CurrentNote.IdentifierString))
                    {
                        if (ImGui.Selectable(Loc.Localize("UseNoteOverlay", "Remove Note Overlay")))
                        {
                            this.CurrentNote = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NeatNoterPlugin.PluginLog.Error(ex, "Failed to draw the overlay window.");
            }
        }
    }
}
