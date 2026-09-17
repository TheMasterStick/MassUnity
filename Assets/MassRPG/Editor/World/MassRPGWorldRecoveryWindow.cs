using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.World;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Crash-recovery browser for the 1x1 World Editor. Recovery copies live under Unity Library,
    /// separate from canonical WorldData. Nothing is restored silently: the designer can inspect,
    /// restore newer recovery pages, or discard stale/local recovery data explicitly.
    /// </summary>
    public sealed class MassRPGWorldRecoveryWindow : EditorWindow
    {
        private sealed class RecoveryEntry
        {
            public WorldPageKey Key;
            public string RecoveryPath;
            public string ProductionPath;
            public DateTime RecoveryWriteUtc;
            public DateTime? ProductionWriteUtc;
            public bool Selected;
            public bool Valid;
            public string Error;

            public bool IsNewerThanProduction
                => !ProductionWriteUtc.HasValue || RecoveryWriteUtc > ProductionWriteUtc.Value;
        }

        private readonly List<RecoveryEntry> _entries = new List<RecoveryEntry>();
        private Vector2 _scroll;
        private bool _showStale = true;
        private string _status = "Not scanned.";

        [MenuItem("MassRPG/World Recovery")]
        public static void Open()
        {
            var window = GetWindow<MassRPGWorldRecoveryWindow>();
            window.titleContent = new GUIContent("MassRPG World Recovery");
            window.minSize = new Vector2(760, 480);
            window.Show();
        }

        private void OnEnable() => Scan();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(58))) Scan();
                if (GUILayout.Button("Select Newer", EditorStyles.toolbarButton, GUILayout.Width(82))) SelectNewer();
                if (GUILayout.Button("Select None", EditorStyles.toolbarButton, GUILayout.Width(72))) SetAll(false);
                GUILayout.Space(8);
                _showStale = GUILayout.Toggle(_showStale, "Show stale/saved copies", EditorStyles.toolbarButton, GUILayout.Width(135));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_entries.Count} recovery page(s)", EditorStyles.miniLabel);
            }

            EditorGUILayout.HelpBox(
                "The World Editor autosaves dirty pages into Unity's local Library folder. A recovery page newer than the canonical WorldData page is a crash/unsaved-work candidate. Restoring validates the page and writes it back into canonical WorldData; stale copies can simply be discarded.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = HasSelected();
                if (GUILayout.Button("Restore Selected to WorldData", GUILayout.Height(28))) RestoreSelected();
                if (GUILayout.Button("Discard Selected Recovery Copies", GUILayout.Height(28))) DiscardSelected();
                GUI.enabled = HasNewer();
                if (GUILayout.Button("Restore All Newer", GUILayout.Height(28))) RestoreAllNewer();
                GUI.enabled = _entries.Count > 0;
                if (GUILayout.Button("Discard All Recovery", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Discard all MassRPG world recovery?",
                        "This deletes all local recovery copies, including any unsaved pages newer than WorldData. Canonical WorldData is not deleted.",
                        "Discard All", "Cancel"))
                    {
                        for (var i = 0; i < _entries.Count; i++) DeleteRecovery(_entries[i]);
                        Scan();
                    }
                }
                GUI.enabled = true;
            }

            GUILayout.Space(4);
            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!_showStale && !entry.IsNewerThanProduction) continue;
                DrawEntry(entry);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Use", EditorStyles.miniBoldLabel, GUILayout.Width(30));
                GUILayout.Label("Page", EditorStyles.miniBoldLabel, GUILayout.Width(155));
                GUILayout.Label("Recovery", EditorStyles.miniBoldLabel, GUILayout.Width(135));
                GUILayout.Label("WorldData", EditorStyles.miniBoldLabel, GUILayout.Width(135));
                GUILayout.Label("State", EditorStyles.miniBoldLabel, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawEntry(RecoveryEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.enabled = entry.Valid;
                entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(30));
                GUI.enabled = true;

                GUILayout.Label(
                    $"P{entry.Key.Plane} F{entry.Key.Storey}  {entry.Key.Page.X},{entry.Key.Page.Y}",
                    GUILayout.Width(155));
                GUILayout.Label(entry.RecoveryWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), EditorStyles.miniLabel, GUILayout.Width(135));
                GUILayout.Label(
                    entry.ProductionWriteUtc.HasValue
                        ? entry.ProductionWriteUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                        : "missing",
                    EditorStyles.miniLabel,
                    GUILayout.Width(135));

                if (!entry.Valid)
                    GUILayout.Label("INVALID", GUILayout.Width(110));
                else if (entry.IsNewerThanProduction)
                    GUILayout.Label("RECOVERY NEWER", GUILayout.Width(110));
                else
                    GUILayout.Label("saved/stale", GUILayout.Width(110));

                GUILayout.FlexibleSpace();
                GUI.enabled = entry.Valid;
                if (GUILayout.Button("Restore", GUILayout.Width(58))) Restore(entry);
                GUI.enabled = true;
                if (GUILayout.Button("Discard", GUILayout.Width(58)))
                {
                    DeleteRecovery(entry);
                    Scan();
                    GUIUtility.ExitGUI();
                }
            }

            if (!entry.Valid && !string.IsNullOrEmpty(entry.Error))
                EditorGUILayout.HelpBox(entry.Error, MessageType.Error);
        }

        private void Scan()
        {
            _entries.Clear();
            var root = WorldPageJsonPersistence.RecoveryRoot;
            if (!Directory.Exists(root))
            {
                _status = "No world-editor recovery folder exists yet.";
                return;
            }

            foreach (var path in Directory.EnumerateFiles(root, "page_*.json", SearchOption.AllDirectories))
            {
                if (!TryParseRecoveryKey(path, out var key)) continue;
                var entry = new RecoveryEntry
                {
                    Key = key,
                    RecoveryPath = path,
                    ProductionPath = WorldPageJsonPersistence.FilePath(key),
                    RecoveryWriteUtc = File.GetLastWriteTimeUtc(path),
                    ProductionWriteUtc = File.Exists(WorldPageJsonPersistence.FilePath(key))
                        ? File.GetLastWriteTimeUtc(WorldPageJsonPersistence.FilePath(key))
                        : (DateTime?)null
                };

                try
                {
                    entry.Valid = WorldPageJsonPersistence.TryLoad(key, out var _, WorldPageJsonPersistence.RecoveryRoot);
                    if (!entry.Valid) entry.Error = "Recovery JSON could not be decoded.";
                }
                catch (Exception ex)
                {
                    entry.Valid = false;
                    entry.Error = ex.Message;
                }
                _entries.Add(entry);
            }

            _entries.Sort((a, b) =>
            {
                var newer = b.IsNewerThanProduction.CompareTo(a.IsNewerThanProduction);
                if (newer != 0) return newer;
                var time = b.RecoveryWriteUtc.CompareTo(a.RecoveryWriteUtc);
                if (time != 0) return time;
                var plane = a.Key.Plane.CompareTo(b.Key.Plane);
                if (plane != 0) return plane;
                var storey = a.Key.Storey.CompareTo(b.Key.Storey);
                if (storey != 0) return storey;
                var y = a.Key.Page.Y.CompareTo(b.Key.Page.Y);
                return y != 0 ? y : a.Key.Page.X.CompareTo(b.Key.Page.X);
            });

            var newerCount = 0;
            var invalidCount = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].Valid) invalidCount++;
                else if (_entries[i].IsNewerThanProduction) newerCount++;
            }
            _status = $"Found {_entries.Count} recovery page(s): {newerCount} newer than WorldData, {invalidCount} invalid.";
            Repaint();
        }

        private void RestoreSelected()
        {
            var count = 0;
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected && Restore(_entries[i], false)) count++;
            _status = $"Restored {count} selected recovery page(s) to WorldData.";
            Scan();
        }

        private void RestoreAllNewer()
        {
            var candidates = 0;
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Valid && _entries[i].IsNewerThanProduction) candidates++;
            if (candidates == 0) return;

            if (!EditorUtility.DisplayDialog(
                "Restore all newer MassRPG recovery pages?",
                $"This will replace/write {candidates} canonical WorldData page(s) with their newer recovery copies.",
                "Restore", "Cancel")) return;

            var restored = 0;
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Valid && _entries[i].IsNewerThanProduction && Restore(_entries[i], false)) restored++;
            _status = $"Restored {restored} newer recovery page(s) to WorldData.";
            Scan();
        }

        private bool Restore(RecoveryEntry entry, bool rescan = true)
        {
            if (!entry.Valid) return false;
            try
            {
                if (!WorldPageJsonPersistence.TryLoad(entry.Key, out var document, WorldPageJsonPersistence.RecoveryRoot))
                    throw new InvalidDataException("Recovery page could not be decoded.");
                WorldPageJsonPersistence.Save(document);
                DeleteRecovery(entry);
                if (rescan)
                {
                    _status = $"Restored page {entry.Key.Page.X},{entry.Key.Page.Y} to WorldData.";
                    Scan();
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"MassRPG recovery restore failed for {entry.RecoveryPath}: {ex}");
                _status = "Restore failed. See Console.";
                return false;
            }
        }

        private void DiscardSelected()
        {
            var selected = 0;
            for (var i = 0; i < _entries.Count; i++) if (_entries[i].Selected) selected++;
            if (selected == 0) return;
            if (!EditorUtility.DisplayDialog(
                "Discard selected MassRPG recovery copies?",
                $"This deletes {selected} local recovery file(s). Canonical WorldData pages are untouched.",
                "Discard", "Cancel")) return;

            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected) DeleteRecovery(_entries[i]);
            Scan();
        }

        private static void DeleteRecovery(RecoveryEntry entry)
        {
            if (File.Exists(entry.RecoveryPath)) File.Delete(entry.RecoveryPath);
        }

        private void SelectNewer()
        {
            for (var i = 0; i < _entries.Count; i++)
                _entries[i].Selected = _entries[i].Valid && _entries[i].IsNewerThanProduction;
            Repaint();
        }

        private void SetAll(bool selected)
        {
            for (var i = 0; i < _entries.Count; i++) _entries[i].Selected = selected && _entries[i].Valid;
            Repaint();
        }

        private bool HasSelected()
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected && _entries[i].Valid) return true;
            return false;
        }

        private bool HasNewer()
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Valid && _entries[i].IsNewerThanProduction) return true;
            return false;
        }

        private static bool TryParseRecoveryKey(string path, out WorldPageKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(path)) return false;

            var storeyDirectory = Directory.GetParent(path);
            var planeDirectory = storeyDirectory != null ? storeyDirectory.Parent : null;
            if (storeyDirectory == null || planeDirectory == null) return false;
            if (!TryParsePrefixedInt(storeyDirectory.Name, "storey_", out var storey)) return false;
            if (!TryParsePrefixedInt(planeDirectory.Name, "plane_", out var plane)) return false;
            return WorldPageJsonPersistence.TryParsePageKeyFromFile(path, plane, storey, out key);
        }

        private static bool TryParsePrefixedInt(string value, string prefix, out int number)
        {
            number = 0;
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(value.Substring(prefix.Length), out number);
        }
    }
}
