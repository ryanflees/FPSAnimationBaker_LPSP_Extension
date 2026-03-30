using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CR
{
    public class SceneBatchGenerator : EditorWindow
    {
        [SerializeField] private Object m_BaseFolder; 
        [SerializeField] private SceneAsset m_ReferenceScene; 

        private Vector3 m_PositionOffset = Vector3.zero;

        [MenuItem("Tools/CR/Batch Scene Generator")]
        public static void OpenWindow()
        {
            var window = GetWindow<SceneBatchGenerator>("Batch Scene Gen");
            window.minSize = new Vector2(400, 250);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("LPSP Batch Scene Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            m_BaseFolder = EditorGUILayout.ObjectField("Source Root Folder", m_BaseFolder, typeof(Object), false);
            m_ReferenceScene = EditorGUILayout.ObjectField("Reference Scene", m_ReferenceScene, typeof(SceneAsset), false) as SceneAsset;
            m_PositionOffset = EditorGUILayout.Vector3Field("Position Offset", m_PositionOffset);
            
            GUILayout.Space(20);

            if (GUILayout.Button("GENERATE BATCH SCENES", GUILayout.Height(40)))
            {
                if (m_BaseFolder == null || m_ReferenceScene == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign both Folder and Reference Scene!", "OK");
                    return;
                }
                ExecuteBatch();
            }
        }

        private void ExecuteBatch()
        {
            string rootPath = AssetDatabase.GetAssetPath(m_BaseFolder);
            string refScenePath = AssetDatabase.GetAssetPath(m_ReferenceScene);

            string[] subDirectories = Directory.GetDirectories(rootPath);
            int total = subDirectories.Length;

            for (int i = 0; i < total; i++)
            {
                string dirPath = subDirectories[i].Replace('\\', '/');
                string dirName = Path.GetFileName(dirPath);

                string newScenePath = $"{dirPath}/{dirName}.unity";

                EditorUtility.DisplayProgressBar("Generating Scenes", $"Processing: {dirName}", (float)i / total);

                if (!File.Exists(newScenePath))
                {
                    AssetDatabase.CopyAsset(refScenePath, newScenePath);
                }
                AssetDatabase.Refresh();

                Scene newScene = EditorSceneManager.OpenScene(newScenePath, OpenSceneMode.Single);

                var tester = Object.FindAnyObjectByType<AnimationBakerLegacyTester>();
                
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dirPath });
                foreach (string guid in guids)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (prefabAsset != null)
                    {
                        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                        if (instance != null)
                        {
                            instance.transform.position += m_PositionOffset;
                            Debug.Log($"[Batch] Added {prefabAsset.name} to {newScenePath}");
                            
                            var sourceHands = instance.GetComponent<SourceFPSHands>();
                            if (sourceHands != null && tester != null)
                            {
                                tester.m_SourceHands = sourceHands;
                                EditorUtility.SetDirty(tester);
                                Debug.Log($"[Batch] Auto-linked SourceFPSHands from {instance.name} to Tester.");
                            }
                        }
                    }
                }

                EditorSceneManager.MarkSceneDirty(newScene);
                EditorSceneManager.SaveScene(newScene);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Success", $"Successfully generated/updated {total} scenes.", "OK");
        }
    }
}
