using UnityEditor;
using UnityEngine;
using MassRPG.Editor.Data;
using MassRPG.Editor.World;

namespace MassRPG.Editor
{
    /// <summary>
    /// Front door for the production authoring suite. Individual tools remain separate windows so
    /// a designer can keep overview/detail/semantic palettes open together on multiple monitors.
    /// </summary>
    public sealed class MassRPGEditorHubWindow : EditorWindow
    {
        [MenuItem("MassRPG/Editor Hub", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<MassRPGEditorHubWindow>();
            window.titleContent = new GUIContent("MassRPG Editor");
            window.minSize = new Vector2(470, 940);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(8);
            GUILayout.Label("MassRPG World Authoring", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "Canonical authoring data lives outside Unity scene objects. Use the whole-world overview for the rough 180k pass, then exact tile/semantic tools for production detail.",
                MessageType.Info);

            Section("Terrain & geography");
            Button("World Overview", "Block the entire 180,000 x 180,000 world at 512x512 storage-page scale.", MassRPGWorldOverviewWindow.Open);
            Button("1x1 World Editor", "Hand-paint exact terrain, elevation, water, pathing, ramps, wall/fence edges and reusable tile stamps.", MassRPGWorldEditorWindow.Open);
            Button("World Recovery", "Inspect autosaved crash-recovery pages; restore only newer unsaved work or discard stale copies.", MassRPGWorldRecoveryWindow.Open);

            Section("World semantics");
            Button("Road Editor", "Draw semantic roads with width, surface and routing/spawn guidance.", MassRPGRoadEditorWindow.Open);
            Button("Area Editor", "Draw overlapping regions, biomes, level bands, faction/resource/no-build/PvP areas.", MassRPGAreaEditorWindow.Open);
            Button("POI Editor", "Place public/hidden POIs with independent visible and protection footprints.", MassRPGPointOfInterestEditorWindow.Open);

            Section("Actors, objects & resources");
            Button("Biome Dressing", "Generate deterministic trees/scenery with density overrides, clearings and sparse manual removals.", MassRPGBiomeDressingEditorWindow.Open);
            Button("Creature Spawns", "Author fixed-cap creature populations, roam areas and optional patrol routes.", MassRPGCreatureSpawnEditorWindow.Open);
            Button("Placement Editor", "Place anchored objects, deliberate resources, NPC anchors, transport nodes and manual doodads.", MassRPGPlacementEditorWindow.Open);

            Section("Game data");
            Button(
                "Content Browser",
                "Search and inspect permanent IDs and migration definitions for items, creatures, recipes, skills and build pieces.",
                MassRPGContentBrowserWindow.Open);
            Button(
                "Repository Drafts",
                "Inspect and validate repository JSON authored from Unity or the browser/Codespaces Data Editor.",
                MassRPGRepositoryDraftsWindow.Open);
            Button(
                "Asset Backlog",
                "See every browser-authored item, monster, resource or other definition still waiting for models, icons, portraits or animation assets.",
                MassRPGAssetBacklogWindow.Open);
            Button(
                "Asset Linker",
                "Attach imported Unity models/icons/portraits/animation assets to work-authored content without rewriting gameplay data.",
                MassRPGPresentationAssetLinkerWindow.Open);
            Button(
                "Build Asset Catalog",
                "Package repository AssetLinks into the compact runtime lookup used by character/equipment presentation.",
                () => PresentationAssetCatalogBuilder.Rebuild(true));
            Button(
                "Materialize Seeds",
                "One-time migration helper: create missing repository drafts from old C# item/creature/recipe seed catalogs without overwriting browser edits.",
                MigrationSeedDraftExporter.MaterializeMigrationSeedDrafts);

            Section("Testing");
            Button(
                "Play From Here",
                "Save the open 1x1 editor and launch an authoritative local play test at its current coordinate.",
                MassRPGPlayFromHereCoordinator.StartFromOpenWorldEditor);

            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(
                "The Online Data Editor can author items/equipment, creatures, resources, recipes and flexible future game definitions without local art files. At home, Asset Backlog + Asset Linker connect imported Unity art to those same permanent IDs; Build Asset Catalog converts those source links into the runtime lookup packaged with the client.",
                MessageType.None);
            GUILayout.Label("Unity 6.3 LTS · MassRPG authored-world tools", EditorStyles.centeredGreyMiniLabel);
        }

        private static void Section(string title)
        {
            GUILayout.Space(8);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        private static void Button(string label, string description, System.Action action)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button(label, GUILayout.Width(145), GUILayout.Height(34))) action();
                GUILayout.Label(description, EditorStyles.wordWrappedMiniLabel, GUILayout.ExpandHeight(true));
            }
        }
    }
}
