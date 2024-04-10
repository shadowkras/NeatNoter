using System.Numerics;

using CheapLoc;
using Dalamud.DrunkenToad.Extensions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace NeatNoter
{
    /// <summary>
    /// Settings window for the plugin.
    /// </summary>
    public class SettingsWindow : PluginWindow
    {
        public bool IsHideConfigurationConfirmationWindowVisible;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
        /// </summary>
        /// <param name="plugin">NeatNoter plugin.</param>
        public SettingsWindow(NeatNoterPlugin plugin)
            : base(plugin, Loc.Localize("Settings", "NeatNoter Settings"))
        {
            this.plugin = plugin;
            this.RespectCloseHotkey = true;
            this.Size = new Vector2(300f, 400f);
            this.SizeCondition = ImGuiCond.Appearing;
            this.IsHideConfigurationConfirmationWindowVisible = false;
        }

        /// <inheritdoc/>
        public override void Draw()
        {
            this.SaveFrequency();
            this.DrawDisplay();
            this.DrawSearch();
        }

        private void SaveFrequency()
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, Loc.Localize("Save", "Save Frequency"));
            ImGui.BeginChild("###Save", new Vector2(-1, 110f), true);
            {
                ImGui.Text(Loc.Localize("SaveFrequency", "Save (seconds)"));
                var saveFrequency = this.plugin.Configuration.SaveFrequency.FromMillisecondsToSeconds();
                if (ImGui.SliderInt("###NeatNoter_SaveFrequency_Slider", ref saveFrequency, 1, 300))
                {
                    this.plugin.Configuration.SaveFrequency = saveFrequency.FromSecondsToMilliseconds();
                    this.plugin.SaveConfig();
                    this.plugin.NotebookService.UpdateSaveFrequency(this.plugin.Configuration.SaveFrequency);
                }

                ImGui.Text(Loc.Localize("FullSaveFrequency", "Full Save (hours)"));
                var fullSaveFrequency = this.plugin.Configuration.FullSaveFrequency.FromMillisecondsToHours();
                if (ImGui.SliderInt("###NeatNoter_FullSaveFrequency_Slider", ref fullSaveFrequency, 1, 12))
                {
                    this.plugin.Configuration.FullSaveFrequency = fullSaveFrequency.FromHoursToMilliseconds();
                    this.plugin.SaveConfig();
                    this.plugin.NotebookService.UpdateFullSaveFrequency(this.plugin.Configuration.FullSaveFrequency);
                }
            }

            ImGui.EndChild();
            ImGui.Spacing();
        }

        private void DrawDisplay()
        {
            if (this.DrawHideConfigurationConfirmationWindow(ref this.IsHideConfigurationConfirmationWindowVisible))
            {
                this.plugin.Configuration.ShowConfigurationTab = false;
                this.plugin.SaveConfig();
            }

            ImGui.TextColored(ImGuiColors.DalamudViolet, Loc.Localize("Display", "Display"));
            ImGui.BeginChild("###Display", new Vector2(-1, 120f), true);
            {
                var showContentPreview = this.plugin.Configuration.ShowContentPreview;
                if (ImGui.Checkbox(Loc.Localize("ShowContentPreview", "Show content preview"), ref showContentPreview))
                {
                    this.plugin.Configuration.ShowContentPreview = showContentPreview;
                    this.plugin.SaveConfig();
                }

                var showConfigurationTab = this.plugin.Configuration.ShowConfigurationTab;
                if (ImGui.Checkbox(Loc.Localize("ShowConfigurationTab", "Show configuration tab"), ref showConfigurationTab))
                {
                    if (!showConfigurationTab)
                    {
                        this.IsHideConfigurationConfirmationWindowVisible = true;
                    }
                    else
                    {
                        this.plugin.Configuration.ShowConfigurationTab = true;
                        this.plugin.SaveConfig();
                    }
                }

                var lockPosition = this.plugin.Configuration.LockPosition;
                if (ImGui.Checkbox(Loc.Localize("LockPosition", "Lock position"), ref lockPosition))
                {
                    this.plugin.Configuration.LockPosition = lockPosition;
                    this.plugin.SaveConfig();
                }

                var lockSize = this.plugin.Configuration.LockSize;
                if (ImGui.Checkbox(Loc.Localize("LockSize", "Lock resize"), ref lockSize))
                {
                    this.plugin.Configuration.LockSize = lockSize;
                    this.plugin.SaveConfig();
                }
            }

            ImGui.EndChild();
            ImGui.Spacing();
        }

        private void DrawSearch()
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, Loc.Localize("Search", "Search"));
            ImGui.BeginChild("###Search", new Vector2(-1, 40f), true);
            {
                var includeBodies = this.plugin.Configuration.IncludeNoteBodiesInSearch;
                if (ImGui.Checkbox(Loc.Localize("IncludeNoteContents", "Include note contents"), ref includeBodies))
                {
                    this.plugin.Configuration.IncludeNoteBodiesInSearch = includeBodies;
                    this.plugin.SaveConfig();
                }
            }

            ImGui.EndChild();
            ImGui.Spacing();
        }

        private bool DrawHideConfigurationConfirmationWindow(ref bool isVisible)
        {
            if (!isVisible)
                return false;

            var ret = false;

            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(350f, 120f), ImGuiCond.Always);
            ImGui.Begin(Loc.Localize("HideConfigConfirmationHeader", "NeatNoter Hide Configuration Confirmation"), ImGuiWindowFlags.NoResize);

            ImGui.Text(Loc.Localize("HideConfigConfirmationSubHeader", "Are you sure you want to hide the configuration tab?"));
            ImGui.Text(Loc.Localize("HideConfigConfirmationInfo", "You can access it using /notebookconf command later."));
            if (ImGui.Button(Loc.Localize("Yes", "Yes")))
            {
                isVisible = false;
                ret = true;
            }

            ImGui.SameLine();
            if (ImGui.Button(Loc.Localize("No", "No")))
            {
                isVisible = false;
            }

            ImGui.End();

            return ret;
        }
    }
}
