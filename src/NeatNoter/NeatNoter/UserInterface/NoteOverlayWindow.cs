using System;

using CheapLoc;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace NeatNoter.NeatNoter.UserInterface
{
    public class NoteOverlayWindow : PluginWindow
    {
        /// <summary>
        /// Currently selected note.
        /// </summary>
        public Note? CurrentNote;

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
            this.Position = null;
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
            this.Position = null;

            try
            {
                if (this.CurrentNote != null)
                {
                    using (var window = ImRaii.Popup("#overlay"))
                    {
                        ImGui.SetWindowFontScale(this.plugin.Configuration.OverlayWindowFontScale);
                        ImGui.TextColored(this.plugin.Configuration.OverlayWindowFontColor, this.CurrentNote.Name + Environment.NewLine + this.CurrentNote.Body);
                        ImGui.SetWindowFontScale(1.0f);

                        if (ImGui.BeginPopupContextItem("###NeatNoter_" + this.CurrentNote.IdentifierString, ImGuiPopupFlags.MouseButtonRight))
                        {
                            if (ImGui.Selectable(Loc.Localize("RemoveNoteOverlay", "Remove as Note Overlay")))
                            {
                                this.CurrentNote = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NeatNoterPlugin.PluginLog.Error(ex, "Failed to draw the overlay window.");
            }
        }

        public void ResetPosition()
        {
            this.Position = new System.Numerics.Vector2(0, 0);
        }
    }
}
