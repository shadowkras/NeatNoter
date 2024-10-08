using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

using CheapLoc;
using Dalamud.DrunkenToad.Helpers;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Logging;
using ImGuiNET;

namespace NeatNoter
{
    /// <summary>
    /// Main window for the plugin.
    /// </summary>
    public class NotebookWindow : PluginWindow
    {
        /// <summary>
        /// Currently selected note.
        /// </summary>
        public Note? CurrentNote;

        /// <summary>
        /// Indicator if note has changed since last save.
        /// </summary>
        public bool IsNoteDirty;

        /// <summary>
        /// Currently selected category.
        /// </summary>
        public Category? CurrentCategory;

        private const int MaxNoteSize = 1024 * 4196; // You can fit the complete works of Shakespeare in 3.5MB, so this is probably fine.
        private static readonly uint TextColor = ImGui.GetColorU32(ImGuiCol.Text);
        private static readonly ushort MaxSafeWordLenght = 2048;

        private SettingsWindow settingsWindow;

        private string exportResult = string.Empty;
        private bool isDeprecationWarningVisible = false;
        private bool categoryWindowVisible;
        private bool deletionWindowVisible;
        private bool backgroundVisible;
        private bool minimalView;
        private bool transparencyWindowVisible;
        private bool textEditable;
        private float editorTransparency;
        private string noteSearchEntry;
        private UIState lastState;
        private UIState state;
        private string previousNote;
        private ImGuiTabItemFlags noteTabFlags;
        private ImGuiTabItemFlags categoryTabFlags;



        /// <summary>
        /// Initializes a new instance of the <see cref="NotebookWindow"/> class.
        /// </summary>
        /// <param name="plugin">NeatNoter plugin.</param>
        public NotebookWindow(NeatNoterPlugin plugin)
            : base(plugin, "NeatNoter")
        {
            this.plugin = plugin;
            this.state = UIState.NoteIndex;
            this.noteSearchEntry = string.Empty;
            this.backgroundVisible = true;
            this.textEditable = true;
            this.Size = new Vector2(400, 600) * ImGui.GetIO().FontGlobalScale;
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.previousNote = string.Empty;
            this.noteTabFlags = ImGuiTabItemFlags.None;
            this.categoryTabFlags = ImGuiTabItemFlags.None;
            this.settingsWindow = new SettingsWindow(this.plugin);
            this.DrawTitleButtons();
            unsafe
            {
                this.editorTransparency = ImGui.GetStyleColorVec4(ImGuiCol.FrameBg)->W;
            }
        }

        private enum UIState
        {
            NoteIndex = 0,
            CategoryIndex = 1,
            NoteEdit = 2,
            CategoryEdit = 3,
            ConfigurationEdit = 99,
        }

        private static float InverseFontScale => 1 / ImGui.GetIO().FontGlobalScale;

        private static float WindowSizeY => ImGui.GetWindowSize().Y * ImGui.GetIO().FontGlobalScale;

        private static float ElementSizeX => ImGui.GetWindowSize().X - (16 * InverseFontScale);

        /// <inheritdoc />
        public override void OnOpen()
        {
            this.plugin.Configuration.IsVisible = true;
            this.plugin.SaveConfig();
        }

        /// <inheritdoc />
        public override void OnClose()
        {
            this.plugin.NotebookService.SaveFullNotebook();
            this.plugin.Configuration.IsVisible = false;
            this.plugin.SaveConfig();
        }

        /// <inheritdoc/>
        public override void Draw()
        {
            try
            {
                this.SetWindowFlags();
                if (this.isDeprecationWarningVisible)
                {
                    this.DisplayDeprecationMessage();
                    return;
                }

                switch (this.state)
                {
                    case UIState.NoteIndex:
                        this.DrawNoteIndex();
                        break;
                    case UIState.CategoryIndex:
                        this.DrawCategoryIndex();
                        break;
                    case UIState.NoteEdit:
                        this.DrawNoteEditTool();
                        break;
                    case UIState.CategoryEdit:
                        this.DrawCategoryEditTool();
                        break;
                    case UIState.ConfigurationEdit:
                        this.DrawConfiguration();
                        break;
                    default:
                        this.DrawNoteIndex();
                        break;
                }
            }
            catch (Exception ex)
            {
                NeatNoterPlugin.PluginLog.Error(ex, "Failed to draw the plugin window.");
            }
        }

        public void DrawTitleButtons(bool undraw = false)
        {
            if (undraw)
            {
                this.TitleBarButtons.Clear();
            }
            else if (this.plugin?.Configuration?.ShowConfigurationButton ?? false)
            {
                this.TitleBarButtons = new()
                {
                    new TitleBarButton()
                    {
                        AvailableClickthrough = true,
                        Icon = FontAwesomeIcon.Cog,
                        Click = (msg) =>
                        {
                            this.plugin.WindowManager.SettingsWindow!.IsOpen ^= true;
                        },
                        IconOffset = new(2,1),
                        ShowTooltip = () =>
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text("Open NeatNoter Settings");
                            ImGui.EndTooltip();
                        },
                    },
                };
            }
        }

        private static bool DrawDeletionConfirmationWindow(ref bool isVisible)
        {
            if (!isVisible)
                return false;

            var ret = false;

            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(280f, 120f));
            ImGui.Begin(Loc.Localize("DeleteConfirmationHeader", "NeatNoter Deletion Confirmation"), ImGuiWindowFlags.NoResize);

            ImGui.Text(Loc.Localize("DeleteConfirmationSubHeader", "Are you sure you want to delete this?"));
            ImGui.Text(Loc.Localize("DeleteConfirmationWarning", "This cannot be undone."));
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

        private static unsafe bool BeginTabItem(string label, ImGuiTabItemFlags flags)
        {
            var unterminatedLabelBytes = Encoding.UTF8.GetBytes(label);
            var labelBytes = stackalloc byte[unterminatedLabelBytes.Length + 1];
            fixed (byte* unterminatedPtr = unterminatedLabelBytes)
            {
                Buffer.MemoryCopy(unterminatedPtr, labelBytes, unterminatedLabelBytes.Length + 1, unterminatedLabelBytes.Length);
            }

            labelBytes[unterminatedLabelBytes.Length] = 0;

            var num2 = (int)ImGuiNative.igBeginTabItem(labelBytes, null, flags);
            return (uint)num2 > 0U;
        }

        private void SetWindowFlags()
        {
            var flags = ImGuiWindowFlags.None;

            if (!this.backgroundVisible)
            {
                flags |= ImGuiWindowFlags.NoBackground;
                flags |= ImGuiWindowFlags.NoTitleBar;
            }

            if (this.plugin.Configuration.LockSize)
            {
                flags |= ImGuiWindowFlags.NoResize;
            }

            if (this.plugin.Configuration.LockPosition)
            {
                flags |= ImGuiWindowFlags.NoMove;
            }

            this.Flags = flags;
        }

        private void DrawTabBar()
        {
            if (ImGui.BeginTabBar("###NeatNoter_TabBar", ImGuiTabBarFlags.NoTooltip))
            {
                if (BeginTabItem(Loc.Localize("Notes", "Notes") + "###NeatNoter_TabItem_Notes", this.noteTabFlags))
                {
                    this.SetState(UIState.NoteIndex);
                    ImGui.EndTabItem();
                }

                if (BeginTabItem(Loc.Localize("Categories", "Categories") + "###NeatNoter_TabItem_Categories", this.categoryTabFlags))
                {
                    this.SetState(UIState.CategoryIndex);
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            this.noteTabFlags = ImGuiTabItemFlags.None;
            this.categoryTabFlags = ImGuiTabItemFlags.None;
        }

        private void DrawConfiguration()
        {
            this.DrawTabBar();
            this.settingsWindow.Draw();
        }

        private void DrawNoteIndex()
        {
            if (DrawDeletionConfirmationWindow(ref this.deletionWindowVisible))
            {
                if (this.CurrentNote != null)
                {
                    this.plugin.NotebookService.DeleteNote(this.CurrentNote);
                    this.CurrentNote = null;
                }
            }

            this.DrawTabBar();

            if (ImGui.Button(Loc.Localize("NewNote", "New Note") + "###NeatNoter_Notes_New"))
            {
                this.CurrentNote = this.plugin.NotebookService.CreateNote();
                this.plugin.NotebookService.SaveNote(this.CurrentNote);
                this.plugin.NotebookService.SortNotes(DocumentSortType.GetDocumentSortTypeByIndex(this.plugin.Configuration.NoteSortType));
                this.SetState(UIState.NoteEdit);
                return;
            }

            var fontScale = ImGui.GetFontSize();

            ImGui.SameLine();
            if (ImGui.Button(Loc.Localize("SortNotes", "Sort") + "###NeatNoter_Notes_Sort"))
            {
                ImGui.OpenPopup("###NeatNoter_Notes_Sort_ContextMenu");
            }

            if (ImGui.BeginPopup("###NeatNoter_Notes_Sort_ContextMenu"))
            {
                if (ImGui.Selectable(DocumentSortType.NameAscending.Name, this.plugin.Configuration.NoteSortType == DocumentSortType.NameAscending.Code))
                {
                    this.plugin.NotebookService.SortNotes(DocumentSortType.NameAscending);
                }
                else if (ImGui.Selectable(DocumentSortType.NameDescending.Name, this.plugin.Configuration.NoteSortType == DocumentSortType.NameDescending.Code))
                {
                    this.plugin.NotebookService.SortNotes(DocumentSortType.NameDescending);
                }
                else if (ImGui.Selectable(DocumentSortType.CreatedAscending.Name, this.plugin.Configuration.NoteSortType == DocumentSortType.CreatedAscending.Code))
                {
                    this.plugin.NotebookService.SortNotes(DocumentSortType.CreatedAscending);
                }
                else if (ImGui.Selectable(DocumentSortType.CreatedDescending.Name, this.plugin.Configuration.NoteSortType == DocumentSortType.CreatedDescending.Code))
                {
                    this.plugin.NotebookService.SortNotes(DocumentSortType.CreatedDescending);
                }
                else if (ImGui.Selectable(DocumentSortType.ModifiedAscending.Name, this.plugin.Configuration.NoteSortType == DocumentSortType.ModifiedAscending.Code))
                {
                    this.plugin.NotebookService.SortNotes(DocumentSortType.ModifiedAscending);
                }
                else if (ImGui.Selectable(DocumentSortType.ModifiedDescending.Name, this.plugin.Configuration.NoteSortType == DocumentSortType.ModifiedDescending.Code))
                {
                    this.plugin.NotebookService.SortNotes(DocumentSortType.ModifiedDescending);
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button(Loc.Localize("FilterNotes", "Filter") + "###NeatNoter_Notes_Filter"))
            {
                ImGui.OpenPopup("###NeatNoter_Notes_Filter_ContextMenu");
            }

            if (ImGui.BeginPopup("###NeatNoter_Notes_Filter_ContextMenu"))
            {
                if (ImGui.Selectable(Loc.Localize("NoCategory", "No Category"), this.plugin.Configuration.IsNoCategorySelected, ImGuiSelectableFlags.DontClosePopups))
                {
                    this.plugin.Configuration.IsNoCategorySelected = !this.plugin.Configuration.IsNoCategorySelected;
                    this.plugin.SaveConfig();
                }

                foreach (var category in this.plugin.NotebookService.GetCategories())
                {
                    if (ImGui.Selectable(category.InternalName, category.IsSelected, ImGuiSelectableFlags.DontClosePopups))
                    {
                        category.IsSelected = !category.IsSelected;
                        this.plugin.NotebookService.SaveCategory(category);
                    }
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine();

            ImGui.SetNextItemWidth(-1);
            ImGui.InputText(string.Empty, ref this.noteSearchEntry, 128);
            ImGui.Separator();

            var notes = this.plugin.NotebookService.GetNotes(true, this.noteSearchEntry).Where(note => note.IsVisible).ToList();
            for (var i = 0; i < notes.Count; i++)
            {
                this.DrawNoteEntry(notes[i], i, ImGui.GetStyle().WindowPadding.Y + ((ImGui.GetStyle().FramePadding.Y * 2) * 3) + (fontScale * 3) + (ImGui.GetStyle().ItemSpacing.Y * 2));
            }
        }

        private void DrawCategoryIndex()
        {
            if (DrawDeletionConfirmationWindow(ref this.deletionWindowVisible))
            {
                if (this.CurrentCategory != null)
                {
                    this.plugin.NotebookService.DeleteCategory(this.CurrentCategory);
                    this.CurrentCategory = null;
                }
            }

            this.DrawTabBar();

            if (ImGui.Button(Loc.Localize("NewCategory", "New Category") + "###NeatNoter_Categories_New"))
            {
                this.CurrentCategory = this.plugin.NotebookService.CreateCategory();
                this.SetState(UIState.CategoryEdit);
                return;
            }

            var fontScale = ImGui.GetFontSize();

            ImGui.SameLine();
            if (ImGui.Button(Loc.Localize("SortCategories", "Sort") + "###NeatNoter_Categories_Sort"))
            {
                ImGui.OpenPopup("###NeatNoter_Categories_Sort_ContextMenu");
            }

            if (ImGui.BeginPopup("###NeatNoter_Categories_Sort_ContextMenu"))
            {
                if (ImGui.Selectable(DocumentSortType.NameAscending.Name, this.plugin.Configuration.CategorySortType == DocumentSortType.NameAscending.Code))
                {
                    this.plugin.NotebookService.SortCategories(DocumentSortType.NameAscending);
                }
                else if (ImGui.Selectable(DocumentSortType.NameDescending.Name, this.plugin.Configuration.CategorySortType == DocumentSortType.NameDescending.Code))
                {
                    this.plugin.NotebookService.SortCategories(DocumentSortType.NameDescending);
                }
                else if (ImGui.Selectable(DocumentSortType.CreatedAscending.Name, this.plugin.Configuration.CategorySortType == DocumentSortType.CreatedAscending.Code))
                {
                    this.plugin.NotebookService.SortCategories(DocumentSortType.CreatedAscending);
                }
                else if (ImGui.Selectable(DocumentSortType.CreatedDescending.Name, this.plugin.Configuration.CategorySortType == DocumentSortType.CreatedDescending.Code))
                {
                    this.plugin.NotebookService.SortCategories(DocumentSortType.CreatedDescending);
                }
                else if (ImGui.Selectable(DocumentSortType.ModifiedAscending.Name, this.plugin.Configuration.CategorySortType == DocumentSortType.ModifiedAscending.Code))
                {
                    this.plugin.NotebookService.SortCategories(DocumentSortType.ModifiedAscending);
                }
                else if (ImGui.Selectable(DocumentSortType.ModifiedDescending.Name, this.plugin.Configuration.CategorySortType == DocumentSortType.ModifiedDescending.Code))
                {
                    this.plugin.NotebookService.SortCategories(DocumentSortType.ModifiedDescending);
                }

                ImGui.EndPopup();
            }

            ImGui.Separator();

            var categories = this.plugin.NotebookService.GetCategories();
            for (var i = 0; i < categories.Count; i++)
            {
                this.DrawCategoryEntry(categories[i], i, ImGui.GetStyle().WindowPadding.Y + ((ImGui.GetStyle().FramePadding.Y * 2) * 3) + (fontScale * 3) + (ImGui.GetStyle().ItemSpacing.Y * 2));
            }
        }

        private void DrawNoteEditTool()
        {
            if (!this.minimalView)
            {
                if (ImGui.Button(Loc.Localize("Back", "Back")))
                {
                    this.SetState(this.lastState); // We won't have menus more then one-deep, so we don't need to set up a push-down
                }

                ImGui.SameLine();
                if (ImGui.Button(
                    this.categoryWindowVisible ? Loc.Localize("CloseCategorySelection", "Close category selection") : Loc.Localize("ChooseCategories", "Choose categories"),
                    new Vector2(ElementSizeX - (60 * ImGui.GetIO().FontGlobalScale), 23 * ImGui.GetIO().FontGlobalScale)))
                    this.categoryWindowVisible = !this.categoryWindowVisible;
                this.CategorySelectionWindow(this.CurrentNote?.Categories);
            }

            this.DrawDocumentEditor(this.CurrentNote);
        }

        private void DrawCategoryEditTool()
        {
            if (!this.minimalView)
            {
                if (ImGui.Button(Loc.Localize("Back", "Back")))
                    this.SetState(this.lastState);

                ImGui.SameLine();
                var color = this.CurrentCategory!.Color;
                if (ImGui.ColorEdit3(Loc.Localize("Color", "Color"), ref color))
                {
                    this.CurrentCategory.Color = color;
                    this.plugin.NotebookService.SaveCategory(this.CurrentCategory);
                }
            }

            this.DrawDocumentEditor(this.CurrentCategory);
        }

        private void DrawNoteEntry(Note note, int index, float heightOffset)
        {
            var fontScale = ImGui.GetFontSize();
            var heightMod = fontScale + ImGui.GetStyle().FramePadding.Y;
            heightOffset += ImGui.GetStyle().ItemSpacing.Y;
            heightOffset -= ImGui.GetScrollY();

            var lineOffset = ElementSizeX * 0.3f;
            var windowPos = ImGui.GetWindowPos();

            var color = note.Categories.Count > 0
                ? new Vector4(note.Categories[0].Color, 0.5f)
                : new Vector4(0.0f, 0.0f, 0.0f, 0.5f);
            ImGui.PushStyleColor(ImGuiCol.Button, color);

            var buttonLabel = note.Name;
            if (this.plugin.Configuration.ShowContentPreview)
            {
                var cutNameLength = Math.Min(note.Name.Length, 70);
                for (var i = 1; i < cutNameLength && ImGui.CalcTextSize(buttonLabel).X > lineOffset - fontScale; i++)
                    buttonLabel = note.Name[..(cutNameLength - i)] + "...";
            }

            if (ImGui.Button(note.IdentifierString, new Vector2(ElementSizeX, heightMod)) && !this.plugin.NotebookService.Loading)
            {
                this.OpenNote(note);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(note.Name);

            ImGui.PopStyleColor();

            var calc = (index * (heightMod + ImGui.GetStyle().ItemSpacing.Y)) + heightOffset;
            var calcFont = calc + (ImGui.GetStyle().FramePadding.Y / 2);

            // Adding the text over manually because otherwise the text position is dependent on label length
            ImGui.GetWindowDrawList().AddText(windowPos + new Vector2(lineOffset - (ElementSizeX / 3.92f), calcFont), TextColor, buttonLabel);

            var contentPreview = string.Empty;
            if (this.plugin.Configuration.ShowContentPreview)
            {
                ImGui.GetWindowDrawList().AddLine(windowPos + new Vector2(lineOffset, calc), windowPos + new Vector2(lineOffset, calc + heightMod), TextColor);
                contentPreview = note.Body.Replace('\n', ' ');
            }

            var cutBodyLength = Math.Min(note.Body.Length, 400);
            for (var i = 1; i < cutBodyLength && ImGui.CalcTextSize(contentPreview).X > ElementSizeX - lineOffset - fontScale; i++)
                contentPreview = note.Body.Replace('\n', ' ')[..(cutBodyLength - i)] + "...";
            ImGui.GetWindowDrawList()
                .AddText(windowPos + new Vector2(lineOffset + 10, calcFont), TextColor, contentPreview);

            if (ImGui.BeginPopupContextItem("###NeatNoter_" + note.IdentifierString))
            {
                if (ImGui.Selectable(Loc.Localize("DeleteNote", "Delete Note")))
                {
                    this.CurrentNote = note;
                    this.deletionWindowVisible = true;
                }

                if (ImGui.Selectable(Loc.Localize("UseNoteOverlay", "Use as Note Overlay")))
                {
                    this.SetNoteOverlay(note);
                }

                ImGui.EndPopup();
            }
        }

        private void OpenNote(Note note)
        {
            this.CurrentNote = note;
            this.SetState(UIState.NoteEdit);
        }

        private void CategorySelectionWindow(ICollection<Category>? selectedCategories)
        {
            if (!this.categoryWindowVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(400, 300) * ImGui.GetIO().FontGlobalScale);
            ImGui.Begin(Loc.Localize("CategorySelection", "Select Category"), ref this.categoryWindowVisible, ImGuiWindowFlags.NoResize);

            var categories = this.plugin.NotebookService.GetCategories();
            if (categories.Count != 0)
            {
                ImGui.Columns(3, "###NeatNoterCategorySelectionColumns", true);
                foreach (var category in categories)
                {
                    var isChecked = selectedCategories!.Contains(category);
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(category.Color, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(category.Color, 1.0f));
                    if (ImGui.Checkbox(category.InternalName, ref isChecked))
                    {
                        if (isChecked)
                            selectedCategories.Add(category);
                        else
                            selectedCategories.Remove(category);
                    }

                    ImGui.PopStyleColor(2);
                    if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(category.Body))
                        ImGui.SetTooltip(category.Body);
                    ImGui.NextColumn();
                }
            }
            else
            {
                ImGui.Text(Loc.Localize("NoCategoriesToChoose", "No categories to choose from."));
            }

            ImGui.End();
        }

        private void DrawCategoryEntry(Category category, int index, float heightOffset)
        {
            var fontScale = ImGui.GetFontSize();
            var heightMod = fontScale + ImGui.GetStyle().FramePadding.Y;
            heightOffset += ImGui.GetStyle().ItemSpacing.Y;
            heightOffset -= ImGui.GetScrollY() * fontScale;

            var color = new Vector4(category.Color, 0.5f);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(color));

            var buttonLabel = category.Name;
            var cutNameLength = Math.Min(category.Name.Length, 70);
            for (var i = 1; i < cutNameLength && ImGui.CalcTextSize(buttonLabel).X > ElementSizeX - 22; i++)
                buttonLabel = category.Name[..(cutNameLength - i)] + "...";
            if (ImGui.Button(category.IdentifierString, new Vector2(ElementSizeX, heightMod)) && !this.plugin.NotebookService.Loading)
            {
                this.OpenCategory(category);
            }

            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(category.Body))
                ImGui.SetTooltip(category.Body);

            ImGui.PopStyleColor();

            var calc = (index * (heightMod + ImGui.GetStyle().ItemSpacing.Y)) + heightOffset;
            var calcFont = calc + (ImGui.GetStyle().FramePadding.Y / 2);

            ImGui.GetWindowDrawList().AddText(ImGui.GetWindowPos() + new Vector2(ElementSizeX * 0.045f, calcFont), TextColor, buttonLabel);

            if (ImGui.BeginPopupContextItem("##NeatNoter_" + category.IdentifierString))
            {
                if (ImGui.Selectable(Loc.Localize("ViewNotes", "View Notes")))
                {
                    this.plugin.NotebookService.SelectOneCategory(category);
                    this.SetState(UIState.NoteIndex);
                    this.noteTabFlags = ImGuiTabItemFlags.SetSelected;
                    this.categoryTabFlags = ImGuiTabItemFlags.None;
                }

                if (ImGui.Selectable(Loc.Localize("EditCategory", "Edit Category")))
                {
                    this.OpenCategory(category);
                }

                if (ImGui.Selectable(Loc.Localize("DeleteCategory", "Delete Category")))
                {
                    this.deletionWindowVisible = true;
                    this.CurrentCategory = category;
                }

                ImGui.EndPopup();
            }
        }

        private void OpenCategory(Category category)
        {
            this.CurrentCategory = category;
            this.SetState(UIState.CategoryEdit);
        }

        private void DrawDocumentEditor(UniqueDocument? document)
        {
            if (this.transparencyWindowVisible)
                this.DrawTransparencySlider(ImGui.GetWindowPos(), ImGui.GetWindowSize());

            if (!this.minimalView)
            {
                var title = document?.Name;
                if (ImGui.InputText(document?.GetTypeName() + " " + Loc.Localize("Title", "Title"), ref title, 128))
                {
                    document!.Name = title;
                    document.Modified = UnixTimestampHelper.CurrentTime();
                    this.IsNoteDirty = true;
                }
            }

            if (!this.backgroundVisible)
            {
                ImGui.Dummy(new Vector2(0, 16.0f) * ImGui.GetIO().FontGlobalScale);
            }

            var body = document?.Body;
            var inputFlags = ImGuiInputTextFlags.AllowTabInput;
            if (!this.textEditable)
                inputFlags |= ImGuiInputTextFlags.ReadOnly;

            Vector4 color;
            unsafe
            {
                color = *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg);
                color.W = this.editorTransparency;
            }

            ImGui.PushStyleColor(ImGuiCol.FrameBg, color);

            if (!this.textEditable) ImGui.GetIO().WantTextInput = false;

            if (ImGui.InputTextMultiline(string.Empty, ref body, MaxNoteSize, new Vector2(ElementSizeX - (16 * ImGui.GetIO().FontGlobalScale), WindowSizeY - (this.minimalView ? 40 : 94) - 25), inputFlags))
            {
                if (document != null)
                {
                    document.Body = body;
                    document.Modified = UnixTimestampHelper.CurrentTime();
                    this.IsNoteDirty = true;
                }
            }

            ImGui.Text($"{document?.WordCount}/{MaxSafeWordLenght}");
            if (document?.WordCount > MaxSafeWordLenght)
                ImGuiComponents.HelpMarker(Loc.Localize("EditorPerformanceLoss", "Warning, performance loss can occur when a note is more than 2048 characters."));

            if (ImGui.IsItemDeactivated() && ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                if (document != null)
                {
                    document.Body = this.previousNote;
                    document.Modified = UnixTimestampHelper.CurrentTime();
                    this.IsNoteDirty = true;
                }
            }

            this.previousNote = body;

            ImGui.PopStyleColor();

            if (!ImGui.BeginPopupContextItem(Loc.Localize("EditorContextMenu", "Editor Context Menu") + " " + document?.InternalName)) return;
            if (!this.minimalView && ImGui.Selectable(Loc.Localize("MinimalView", "Minimal view")))
            {
                this.minimalView = true;
            }
            else if (this.minimalView && ImGui.Selectable(Loc.Localize("EndMinimalView", "End minimal view")))
            {
                this.minimalView = false;
            }

            if (this.backgroundVisible && ImGui.Selectable(Loc.Localize("HideBackground", "Hide background")))
            {
                this.backgroundVisible = false;
            }
            else if (!this.backgroundVisible && ImGui.Selectable(Loc.Localize("ShowBackground", "Show background")))
            {
                this.backgroundVisible = true;
            }

            if (this.textEditable && ImGui.Selectable(Loc.Localize("TextEditDisable", "Disable text editing")))
            {
                this.textEditable = false;
            }
            else if (!this.textEditable && ImGui.Selectable(Loc.Localize("TextEditEnable", "Enable text editing")))
            {
                this.textEditable = true;
            }

            if (!this.transparencyWindowVisible && ImGui.Selectable(Loc.Localize("ShowEditorTransparencySlider", "Show editor transparency slider")))
            {
                this.transparencyWindowVisible = true;
            }
            else if (this.transparencyWindowVisible && ImGui.Selectable(Loc.Localize("HideEditorTransparencySlider", "Hide editor transparency slider")))
            {
                this.transparencyWindowVisible = false;
            }

            if (this.plugin?.WindowManager?.NoteOverlayWindow?.CurrentNote != this.CurrentNote && ImGui.Selectable(Loc.Localize("SetNoteOverlay", "Set as note overlay")))
            {
                this.SetNoteOverlay(this.CurrentNote);
            }
            else if (this.transparencyWindowVisible && ImGui.Selectable(Loc.Localize("RemoveNoteOverlay", "Remove as note overlay")))
            {
                this.SetNoteOverlay(null);
            }

            ImGui.EndPopup();
        }

        private void DrawTransparencySlider(Vector2 mainWinPos, Vector2 mainWinSize)
        {
            var flags = ImGuiWindowFlags.AlwaysAutoResize;
            if (!this.backgroundVisible)
            {
                flags |= ImGuiWindowFlags.NoBackground;
                flags |= ImGuiWindowFlags.NoTitleBar;
            }

            ImGui.SetNextWindowPos(mainWinPos + new Vector2(0, mainWinSize.Y + (8 * ImGui.GetIO().FontGlobalScale)), ImGuiCond.Always);
            ImGui.Begin(Loc.Localize("TransparencySlider", "Transparency Slider") + "##51454623463", flags);
            {
                var label = "##51454623464";
                if (!this.backgroundVisible)
                {
                    label = Loc.Localize("TransparencySlider", "Transparency Slider") + label;
                }

                ImGui.DragFloat(label, ref this.editorTransparency, 0.01f, 0, 1);
            }

            ImGui.End();
        }

        private void SetState(UIState newState)
        {
            if (newState == this.state)
                return;

            this.plugin.SaveConfig();

            this.deletionWindowVisible = false;
            this.categoryWindowVisible = false;
            this.backgroundVisible = true;

            this.lastState = this.state;
            this.state = newState;
        }

        private void DisplayDeprecationMessage()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
            ImGui.TextWrapped("NeatNoter will stop working with the Dawntrail launch and won't get updates.\n" +
                              "I'm no longer interested in supporting the plugin and I haven't been able to find a developer to take over.\n" +
                              "Please use the Export Button below to save your notes as a CSV file.\n" +
                              "Look into other plugins like NOTED or other note taking tools.\n" +
                              "You'll see this message when the game starts, but you can access your notes by clicking OK.\n" +
                              "Thank you for using NeatNoter!");
            ImGui.PopStyleColor();

            ImGui.Spacing();
            if (ImGui.Button("Export"))
            {
                this.exportResult = this.plugin.NotebookService.ExportNotes();
            }

            ImGui.SameLine();
            if (ImGui.Button("OK"))
            {
                this.isDeprecationWarningVisible = false;
            }

            ImGui.Text(this.exportResult);

            return;
        }

        private void SetNoteOverlay(Note? note)
        {
            if (this.plugin.WindowManager.NoteOverlayWindow != null)
            {
                this.plugin.WindowManager.NoteOverlayWindow.CurrentNote = note;

                if (note is not null && !this.plugin.WindowManager.NoteOverlayWindow.IsOpen)
                    this.plugin.WindowManager.NoteOverlayWindow.Toggle();
                else if (note is null && this.plugin.WindowManager.NoteOverlayWindow.IsOpen)
                    this.plugin.WindowManager.NoteOverlayWindow.Toggle();
            }
        }
    }
}
