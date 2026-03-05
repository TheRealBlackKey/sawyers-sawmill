using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Linq;

namespace Sawmill.Editor
{
    public class BackgroundAutoFixer
    {
        [MenuItem("Sawmill/Auto Fix Background & Layers")]
        public static void RunFix()
        {
            var bg = Object.FindAnyObjectByType<Sawmill.Core.WorldTileManager>();
            if (bg != null)
            {
                // Find all Sprites in Map Tiles folder
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Sprites/World/Map Tiles" });
                
                // We must use LoadAllAssetsAtPath to get the individual SLICES, not just the parent Texture2D
                var allSprites = new System.Collections.Generic.List<Sprite>();
                foreach(var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    
                    var spritesInAsset = assets.OfType<Sprite>().ToList();
                    
                    // If this is a sliced sprite sheet (multiple sprites), Unity includes 
                    // a Sprite for the entire root sheet. We need to filter that out.
                    if (spritesInAsset.Count > 1)
                    {
                        string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                        spritesInAsset.RemoveAll(s => s.name == assetName);
                    }
                    
                    allSprites.AddRange(spritesInAsset);
                }

                var waterSprites = allSprites.Where(s => s.name.ToLower().Contains("river")).ToArray();
                
                // Strict user-requested nomenclature filtering
                var grassSprites = allSprites.Where(s => s.name.ToLower().Contains("grass")).ToArray();
                var flowerSprites = allSprites.Where(s => s.name.ToLower().Contains("flower")).ToArray();
                var stickSprites = allSprites.Where(s => s.name.ToLower().Contains("stick") || s.name.ToLower().Contains("leaf")).ToArray();
                var rubbleSprites = allSprites.Where(s => s.name.ToLower().Contains("rubble")).ToArray();
                var rockSprites = allSprites.Where(s => s.name.ToLower().Contains("rock") && !s.name.ToLower().Contains("rubble")).ToArray();

                if (grassSprites.Length > 0 || waterSprites.Length > 0)
                {
                    Undo.RecordObject(bg, "Auto Assign Background Tiles");
                    var so = new SerializedObject(bg);
                    
                    PopulateArray(so, "waterTiles", waterSprites);
                    PopulateArray(so, "level1Grass", grassSprites);
                    
                    // Fallbacks for the missing decor tiers: just use random generic grass slices
                    PopulateArray(so, "level2Flowers", flowerSprites.Length > 0 ? flowerSprites : grassSprites.OrderBy(x => Random.value).Take(5).ToArray());
                    PopulateArray(so, "level3Sticks", stickSprites.Length > 0 ? stickSprites : grassSprites.OrderBy(x => Random.value).Take(5).ToArray());
                    PopulateArray(so, "level4Rubble", rubbleSprites.Length > 0 ? rubbleSprites : grassSprites.OrderBy(x => Random.value).Take(5).ToArray());
                    PopulateArray(so, "level5Rocks", rockSprites.Length > 0 ? rockSprites : grassSprites.OrderBy(x => Random.value).Take(5).ToArray());

                    so.ApplyModifiedProperties();
                    Debug.Log($"[AutoFixer] Successfully assigned {grassSprites.Length} grass tiles, {flowerSprites.Length} flowers, and {waterSprites.Length} water tiles to WorldTileManager on the Grid.");
                }
            }

            // Force Camera to solid background color so gaps look like grid lines
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0.12f, 0.12f, 0.12f); // Dark grid line color
            }

            Debug.Log("[AutoFixer] Execution complete.");
        }

        private static void PopulateArray(SerializedObject so, string propertyName, Sprite[] sprites)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;
            
            prop.ClearArray();
            for (int i = 0; i < sprites.Length; i++) 
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        [MenuItem("Sawmill/Slice Grass Map")]
        public static void SliceGrassMap()
        {
            string path = "Assets/Sprites/World/Map Tiles/Grass_Main.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError("Could not find Grass_Main.png at " + path);
                return;
            }

            importer.isReadable = true;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            int width = 2048;
            int height = 2048;
            int sliceSize = 32;

            int cols = width / sliceSize;
            int rows = height / sliceSize;

            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            
            var spriteRects = new System.Collections.Generic.List<UnityEditor.SpriteRect>();
            
            int count = 0;
            // Slice top to bottom, left to right
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int col = 0; col < cols; col++)
                {
                    spriteRects.Add(new UnityEditor.SpriteRect
                    {
                        name = "Grass_Main_" + count,
                        spriteID = GUID.Generate(),
                        rect = new Rect(col * sliceSize, row * sliceSize, sliceSize, sliceSize),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = SpriteAlignment.Center,
                        border = Vector4.zero
                    });
                    count++;
                }
            }

            dataProvider.SetSpriteRects(spriteRects.ToArray());
            dataProvider.Apply();
            importer.SaveAndReimport();
            Debug.Log($"Successfully sliced {path} into {count} tiles.");
        }
    }
}
