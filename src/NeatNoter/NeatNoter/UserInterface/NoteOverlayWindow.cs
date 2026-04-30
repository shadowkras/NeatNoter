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
                        var text = this.CurrentNote.Name + Environment.NewLine + this.CurrentNote.Body;

                        // Apply font scale BEFORE measuring
                        ImGui.SetWindowFontScale(this.plugin.Configuration.OverlayWindowFontScale);

                        var textSize = ImGui.CalcTextSize(text);

                        System.Numerics.Vector2 padding = new System.Numerics.Vector2(10f, 10f);
                        System.Numerics.Vector2 childSize = textSize + padding;

                        ImGui.PushStyleColor(ImGuiCol.ChildBg, this.plugin.Configuration.OverlayWindowBackgroundColor);

                        // Child with computed size
                        using (ImRaii.Child("###NeatNoter_OverlayChild", childSize, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar))
                        {
                            ImGui.TextColored(this.plugin.Configuration.OverlayWindowFontColor, text);

                            if (ImGui.BeginPopupContextItem("###NeatNoter_" + this.CurrentNote.IdentifierString, ImGuiPopupFlags.MouseButtonRight))
                            {
                                if (ImGui.Selectable(Loc.Localize("RemoveNoteOverlay", "Remove as Note Overlay")))
                                {
                                    this.CurrentNote = null;
                                }
                            }
                        }

                        ImGui.PopStyleColor();

                        // Reset scale AFTER everything
                        ImGui.SetWindowFontScale(1.0f);
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
