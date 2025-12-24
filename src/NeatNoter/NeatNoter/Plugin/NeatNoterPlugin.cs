using System.Reflection;
using System.Threading;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NeatNoter.Localization;

namespace NeatNoter
{
    /// <inheritdoc />
    public class NeatNoterPlugin : IDalamudPlugin
    {
        /// <summary>
        /// Plugin configuration.
        /// </summary>
        public readonly NeatNoterConfiguration Configuration;

        /// <summary>
        /// Notebook.
        /// </summary>
        public readonly NotebookService NotebookService;

        /// <summary>
        /// Window Manager.
        /// </summary>
        public readonly WindowManager WindowManager = null!;

        /// <summary>
        /// Backup manager.
        /// </summary>
        public BackupManager BackupManager;

        private Timer? backupTimer;
        private readonly LegacyLoc localization;

        /// <summary>
        /// Initializes a new instance of the <see cref="NeatNoterPlugin"/> class.
        /// </summary>
        /// <param name="dalamudPluginInterface">Instance of IDalamudPluginInterface.</param>
        /// <param name="pluginLog">Instance of IPluginLog.</param>
        public NeatNoterPlugin(IDalamudPluginInterface dalamudPluginInterface, IPluginLog pluginLog)
        {
            PluginInterface = dalamudPluginInterface;
            PluginLog = pluginLog;

            // load config
            try
            {
                this.Configuration = PluginInterface.GetPluginConfig() as NeatNoterConfiguration ??
                                     new NeatNoterConfiguration();
            }
            catch (Exception ex)
            {
                PluginLog.Error("Failed to load config so creating new one.", ex);
                this.Configuration = new NeatNoterConfiguration();
                this.SaveConfig();
            }

            // load services
            this.localization = new LegacyLoc(PluginInterface, CommandManager);
            this.BackupManager = new BackupManager(PluginInterface.GetPluginConfigDirectory());
            this.NotebookService = new NotebookService(this);

            // run backup
            this.backupTimer = new Timer(
                this.BackupTimerCallback,
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            this.backupTimer.Change(
                TimeSpan.FromMilliseconds(this.Configuration.BackupFrequency),
                TimeSpan.FromMilliseconds(this.Configuration.BackupFrequency));
            var pluginVersion = GetPluginVersionNumber();
            if (this.Configuration.PluginVersion < pluginVersion)
            {
                PluginLog.Warning("Running backup since new version detected.");
                this.RunUpgradeBackup();
                this.Configuration.PluginVersion = pluginVersion;
                this.SaveConfig();
            }
            else
            {
                this.BackupTimerOnElapsed();
            }

            // attempt to migrate if needed
            var success = Migrator.Migrate(this);
            if (success)
            {
                this.HandleJustInstalled();
                this.NotebookService.Start();
                this.backupTimer?.Change(
                    TimeSpan.FromMilliseconds(this.Configuration.BackupFrequency),
                    TimeSpan.FromMilliseconds(this.Configuration.BackupFrequency));
                DocumentSortType.Init();
                this.WindowManager = new WindowManager(this);
                this.PluginCommandManager = new PluginCommandManager(this);
            }
        }

        /// <summary>
        /// Gets pluginInterface.
        /// </summary>
        [PluginService]
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        /// <summary>
        /// Gets pluginInterface.
        /// </summary>
        [PluginService]
        public static IPluginLog PluginLog { get; private set; } = null!;

        /// <summary>
        /// Gets chat gui.
        /// </summary>
        [PluginService]
        public static IChatGui Chat { get; private set; } = null!;

        /// <summary>
        /// Gets command manager.
        /// </summary>
        [PluginService]
        public static ICommandManager CommandManager { get; private set; } = null!;

        /// <summary>
        /// Gets client state.
        /// </summary>
        [PluginService]
        public static IClientState ClientState { get; private set; } = null!;

        /// <summary>
        /// Gets or sets command manager to handle user commands.
        /// </summary>
        public PluginCommandManager PluginCommandManager { get; set; } = null!;

        /// <summary>
        /// Get plugin folder.
        /// </summary>
        /// <returns>plugin folder name.</returns>
        public static string GetPluginFolder()
        {
            return PluginInterface.GetPluginConfigDirectory();
        }

        public static int GetPluginVersionNumber()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

            return
                (v.Major * 1_000_000) +
                (v.Minor * 1_000) +
                v.Build;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Save configuration.
        /// </summary>
        public void SaveConfig()
        {
            PluginInterface.SavePluginConfig(this.Configuration);
        }

        /// <summary>
        /// Dispose plugin.
        /// </summary>
        /// <param name="disposing">indicator whether disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    this.WindowManager.Dispose();

                    // Stop timer first (optional but clean)
                    this.backupTimer?.Change(
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan);

                    this.backupTimer?.Dispose();
                    this.PluginCommandManager.Dispose();
                    PluginInterface.SavePluginConfig(this.Configuration);
                    this.NotebookService.Dispose();
                    this.localization.Dispose();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to dispose properly.");
            }
        }

        private void HandleJustInstalled()
        {
            if (!this.Configuration.JustInstalled)
            {
                return;
            }

            this.NotebookService.SetVersion(2);
            this.Configuration.JustInstalled = false;
            this.SaveConfig();
        }

        private void BackupTimerCallback(object? state)
        {
            this.BackupTimerOnElapsed();
        }

        private void BackupTimerOnElapsed()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (now > this.Configuration.LastBackup + this.Configuration.BackupFrequency)
            {
                PluginLog.Warning("Running backup due to frequency timer.");

                this.Configuration.LastBackup = now;

                this.BackupManager.CreateBackup();
                this.BackupManager.DeleteBackups(this.Configuration.BackupRetention);
            }
        }

        private void RunUpgradeBackup()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            this.Configuration.LastBackup = timestamp;
            this.BackupManager.CreateBackup("upgrade/v" + this.Configuration.PluginVersion + "_");
            this.BackupManager.DeleteBackups(this.Configuration.BackupRetention);
        }

        
    }
}
