using Dalamud.Interface.Windowing;
using NeatNoter.NeatNoter.UserInterface;

namespace NeatNoter
{
    /// <summary>
    /// Window manager to hold plugin windows and window system.
    /// </summary>
    public class WindowManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowManager"/> class.
        /// </summary>
        /// <param name="plugin">NeatNoter plugin.</param>
        public WindowManager(NeatNoterPlugin plugin)
        {
            // create windows
            this.NotebookWindow = new NotebookWindow(plugin);
            this.NoteOverlayWindow = new NoteOverlayWindow(plugin);
            this.SettingsWindow = new SettingsWindow(plugin);

            // setup window system
            this.WindowSystem = new WindowSystem("NeatNoterWindowSystem");
            this.WindowSystem.AddWindow(this.NotebookWindow);
            this.WindowSystem.AddWindow(this.NoteOverlayWindow);
            this.WindowSystem.AddWindow(this.SettingsWindow);
            this.NotebookWindow.IsOpen = plugin.Configuration.IsVisible;
            NeatNoterPlugin.PluginInterface.UiBuilder.OpenMainUi += this.DrawMainUi;
            NeatNoterPlugin.PluginInterface.UiBuilder.Draw += this.Draw;
            NeatNoterPlugin.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
        }

        /// <summary>
        /// Gets main NeatNoter window.
        /// </summary>
        public NotebookWindow? NotebookWindow { get; }

        /// <summary>
        /// Gets NeatNoter overlay window.
        /// </summary>
        public NoteOverlayWindow? NoteOverlayWindow { get; }

        /// <summary>
        /// Gets config NeatNoter window.
        /// </summary>
        public SettingsWindow? SettingsWindow { get; }

        private WindowSystem WindowSystem { get; }

        /// <summary>
        /// Dispose plugin windows and commands.
        /// </summary>
        public void Dispose()
        {
            NeatNoterPlugin.PluginInterface.UiBuilder.OpenMainUi -= this.DrawMainUi;
            NeatNoterPlugin.PluginInterface.UiBuilder.Draw -= this.Draw;
            NeatNoterPlugin.PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
            this.WindowSystem.RemoveAllWindows();
        }

        private void DrawMainUi()
        {
            // only show when logged in
            if (!NeatNoterPlugin.ClientState.IsLoggedIn) return;

            if (this.NotebookWindow is not null)
                this.NotebookWindow.IsOpen = true;
        }

        private void Draw()
        {
            // only show when logged in
            if (!NeatNoterPlugin.ClientState.IsLoggedIn) return;
            this.WindowSystem.Draw();
        }

        private void OpenConfigUi()
        {
            this.SettingsWindow!.IsOpen ^= true;
        }
    }
}
