using System;
using System.Collections.Generic;
using System.IO;
using VisualEQ.Database.Configuration;

namespace VisualEQ.Settings
{
    public class AppSettings
    {
        public DatabaseSettings Database { get; set; } = new DatabaseSettings
        {
            Server = "localhost",
            Port = 3306,
            Database = "",
            Username = "",
            Password = "",
            ConnectionTimeout = 10
        };

        public bool ShowPathGrids { get; set; } = true;
        public bool ShowSpawnList { get; set; } = true;

        // Root of the user's EverQuest install (used by the in-app converter). Replaces eq_config.txt.
        public string EqInstallPath { get; set; } = @"C:\Program Files (x86)\EverQuest";

        // Where the in-app converter writes `{zone}_oes.zip` / `{zone}_chr_oes.zip` and where
        // the app looks for them at load time. Defaults to %APPDATA%\VisualEQ\zones\ so a
        // packaged (self-contained) install doesn't try to write into Program Files. The old
        // location (`../ConverterApp/`) is still honored if the user copies existing zips into
        // this directory manually.
        public string ConvertedAssetsPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualEQ", "zones");

        // Sidebar layout persistence. Width in pixels. Section order is a list of section IDs
        // (see SidebarWidget.Section* constants) — empty list means "use default order".
        public float SidebarWidth { get; set; } = 380f;
        public List<string> SidebarSectionOrder { get; set; } = new List<string>();

        // Spawn state markers — vertical colored lines above spawns (see SpawnMarkers).
        public bool ShowPlaceholderMarkers { get; set; } = true;
        public bool ShowDirtyMarkers { get; set; } = true;
        public bool ShowSelectedMarker { get; set; } = true;

        // Edit mode: when false (default), drag operations are disabled — the app is a viewer.
        // When true, spawns and grid waypoints can be dragged, edits accumulate in a per-zone
        // JSON buffer at %APPDATA%\VisualEQ\pending\<zone>.json until committed to DB.
        public bool EditModeEnabled { get; set; } = false;
    }
}
