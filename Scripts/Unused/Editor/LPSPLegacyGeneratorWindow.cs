using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace CR
{
    public class LPSPLegacyGenerator : EditorWindow
    {
        [SerializeField] private Object m_InputFolder; 
        [SerializeField] private Object m_OutputFolder; 

        [MenuItem("Tools/CR/LPSP Legacy Generator")]
        public static void OpenWindow() => GetWindow<LPSPLegacyGenerator>("Legacy Gen");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("LPSP Legacy FPS Weapon Generator", EditorStyles.boldLabel);
            m_InputFolder = EditorGUILayout.ObjectField("Input FBX Folder", m_InputFolder, typeof(Object), false);
            m_OutputFolder = EditorGUILayout.ObjectField("Output Root Folder", m_OutputFolder, typeof(Object), false);

            if (GUILayout.Button("GENERATE ALL", GUILayout.Height(40)))
            {
                if (m_InputFolder == null || m_OutputFolder == null) return;
                Execute();
            }
        }

        private void Execute()
        {
            string inputPath = AssetDatabase.GetAssetPath(m_InputFolder);
            string outputPath = AssetDatabase.GetAssetPath(m_OutputFolder);

            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { inputPath });

            foreach (string guid in fbxGuids)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!fbxPath.ToLower().EndsWith(".fbx")) continue;

                GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                string cleanName = fbxAsset.name.Replace("(Legacy)", "").Trim();

                string subFolder = Path.Combine(outputPath, cleanName).Replace("\\", "/");
                if (!AssetDatabase.IsValidFolder(subFolder))
                    AssetDatabase.CreateFolder(outputPath, cleanName);

                AnimatorController controller = CreateController(fbxPath, subFolder, cleanName);

                CreateLegacyPrefab(fbxAsset, controller, subFolder, cleanName);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done", "Legacy weapons generated successfully!", "OK");
        }

        private AnimatorController CreateController(string fbxPath, string folder, string name)
        {
            string path = Path.Combine(folder, name + "_Controller.controller").Replace("\\", "/");
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var rootLayer = controller.layers[0];
            var stateMachine = rootLayer.stateMachine;

            int statesPerRow = 5;
            float spacingX = 250f;
            float spacingY = 70f;

            var clips = subAssets.OfType<AnimationClip>()
                .Where(c => !c.name.Contains("__preview__"))
                .ToList();

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];

                string stateName = clip.name;
                if (stateName.Contains("@"))
                {
                    stateName = stateName.Split('@')[0];
                }

                int row = i / statesPerRow;
                int col = i % statesPerRow;
                Vector2 statePosition = new Vector2(col * spacingX, row * spacingY);

                AnimatorState state = stateMachine.AddState(stateName, statePosition);
                state.motion = clip;

                if (stateName.ToLower().Contains("idle"))
                {
                    stateMachine.defaultState = state;
                }
            }

            stateMachine.entryPosition = new Vector2(-250, 0);
            stateMachine.anyStatePosition = new Vector2(-250, 50);

            return controller;
        }

        private void CreateLegacyPrefab(GameObject fbxAsset, AnimatorController controller, string folder, string name)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(fbxAsset) as GameObject;
            instance.name = name;

            SourceFPSHands hands = instance.AddComponent<SourceFPSHands>();
            Animator anim = instance.GetComponent<Animator>();
            if (anim != null) anim.runtimeAnimatorController = controller;
            hands.m_Animator = anim;

            hands.m_LeftShoulder = FindChildRecursive(instance.transform, "arm_L");
            hands.m_LeftElbow = FindChildRecursive(instance.transform, "lower_arm_L");
            hands.m_LeftHand = FindChildRecursive(instance.transform, "hand_L");

            hands.m_RightShoulder = FindChildRecursive(instance.transform, "arm_R");
            hands.m_RightElbow = FindChildRecursive(instance.transform, "lower_arm_R");
            hands.m_RightHand = FindChildRecursive(instance.transform, "hand_R");

            Transform armsRoot = FindChildRecursive(instance.transform, "arms");
            if (armsRoot != null)
            {
                Renderer armsRenderer = armsRoot.GetComponent<Renderer>();
                if (armsRenderer != null) hands.m_HandRenderers.Add(armsRenderer);

                for (int i = 0; i < armsRoot.childCount; i++)
                {
                    Transform child = armsRoot.GetChild(i);

                    Renderer[] childRenderers = child.GetComponentsInChildren<Renderer>(true);
                    hands.m_WeaponRenderers.AddRange(childRenderers);

                    for (int n = 0; n < childRenderers.Length; n++)
                    {
                        string lowerName = childRenderers[n].gameObject.name.ToLower();
                        if (lowerName.Contains("knife") || lowerName.Contains("scope"))
                        {
                            childRenderers[n].gameObject.SetActive(false);
                        }
                    }
                }
            }

            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    hands.m_AnimationCellList.Add(new AnimationCellData
                    {
                        m_Layer = 0, 
                        m_CellName = state.state.name,
                        m_MainAnimatorState = state.state.name
                    });
                }
            }

            string prefabPath = Path.Combine(folder, name + ".prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            DestroyImmediate(instance);
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform result = FindChildRecursive(child, name);
                if (result != null) return result;
            }

            return null;
        }
    }
}
