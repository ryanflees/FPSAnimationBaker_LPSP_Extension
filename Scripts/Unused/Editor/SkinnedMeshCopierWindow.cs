using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace CR
{
	public class SkinnedMeshCopierWindow : EditorWindow
	{
		private GameObject m_TargetRoot;
		private List<SkinnedMeshRenderer> m_SelectedSMRs = new List<SkinnedMeshRenderer>();
		private List<MeshRenderer> m_SelectedMRs = new List<MeshRenderer>();

		[MenuItem("Tools/CR/Skinned Mesh Copier")]
		public static void Open()
		{
			var window = GetWindow<SkinnedMeshCopierWindow>("SMR Copier");
			window.minSize = new Vector2(400, 300);
			window.Show();
		}

		private void OnGUI()
		{
			GUILayout.Space(10);
			EditorGUILayout.LabelField("Mesh Copier & Sync", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Select Renderers in Hierarchy. They will share a single hierarchy in the Target Root and sync locally via FPSBoneSynchronizer.", MessageType.Info);

			GUILayout.Space(10);
			m_TargetRoot = (GameObject)EditorGUILayout.ObjectField("Target Root", m_TargetRoot, typeof(GameObject), true);

			GUILayout.Space(10);
			if (GUILayout.Button("Load Selected Renderers from Selection", GUILayout.Height(25)))
			{
				m_SelectedSMRs.Clear();
				m_SelectedMRs.Clear();
				foreach (var obj in Selection.gameObjects)
				{
					// Load SkinnedMeshRenderers
					var childSmrs = obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
					foreach (var smr in childSmrs)
					{
						if (!m_SelectedSMRs.Contains(smr)) m_SelectedSMRs.Add(smr);
					}

					// Load MeshRenderers
					var childMrs = obj.GetComponentsInChildren<MeshRenderer>(true);
					foreach (var mr in childMrs)
					{
						if (!m_SelectedMRs.Contains(mr)) m_SelectedMRs.Add(mr);
					}
				}
			}

			EditorGUILayout.LabelField($"SMRs: {m_SelectedSMRs.Count} | MRs: {m_SelectedMRs.Count}", EditorStyles.miniBoldLabel);
			
			if (m_SelectedSMRs.Count > 0 || m_SelectedMRs.Count > 0)
			{
				GUILayout.BeginVertical("box");
				foreach (var smr in m_SelectedSMRs) if (smr != null) EditorGUILayout.LabelField("- [SMR] " + smr.name, EditorStyles.miniLabel);
				foreach (var mr in m_SelectedMRs) if (mr != null) EditorGUILayout.LabelField("- [MR] " + mr.name, EditorStyles.miniLabel);
				GUILayout.EndVertical();
			}

			GUILayout.Space(20);
			GUI.enabled = m_TargetRoot != null && (m_SelectedSMRs.Count > 0 || m_SelectedMRs.Count > 0);
			if (GUILayout.Button("COPY AND SYNC ALL", GUILayout.Height(40)))
			{
				CopyAndSync();
			}
			GUI.enabled = true;
		}

		private void CopyAndSync()
		{
			Undo.IncrementCurrentGroup();
			int groupIndex = Undo.GetCurrentGroup();

			// 1. Ensure target root has a bone synchronizer
			FPSBoneSynchronizer synchronizer = m_TargetRoot.GetComponent<FPSBoneSynchronizer>();
			if (synchronizer == null)
			{
				synchronizer = m_TargetRoot.AddComponent<FPSBoneSynchronizer>();
				Undo.RegisterCreatedObjectUndo(synchronizer, "Add Synchronizer");
			}

			// 2. Create a single container for all copied meshes
			GameObject meshContainer = new GameObject("Copied_Meshes");
			meshContainer.transform.SetParent(m_TargetRoot.transform);
			meshContainer.transform.localPosition = Vector3.zero;
			meshContainer.transform.localRotation = Quaternion.identity;
			Undo.RegisterCreatedObjectUndo(meshContainer, "Create Mesh Container");

			// --- Copy SkinnedMeshRenderers ---
			foreach (var sourceSMR in m_SelectedSMRs)
			{
				if (sourceSMR == null) continue;

				Animator sourceAnimator = sourceSMR.GetComponentInParent<Animator>();
				if (sourceAnimator == null) continue;

				GameObject newSMRGO = Instantiate(sourceSMR.gameObject, meshContainer.transform);
				newSMRGO.name = sourceSMR.name + "_Mesh";
				Undo.RegisterCreatedObjectUndo(newSMRGO, "Copy SMR Mesh");

				SkinnedMeshRenderer newSMR = newSMRGO.GetComponent<SkinnedMeshRenderer>();
				Transform[] sourceBones = sourceSMR.bones;
				Transform[] newBones = new Transform[sourceBones.Length];

				for (int i = 0; i < sourceBones.Length; i++)
				{
					Transform sourceBone = sourceBones[i];
					if (sourceBone == null) continue;
					newBones[i] = GetOrCreateBoneInTarget(sourceBone, sourceAnimator.transform, m_TargetRoot.transform, synchronizer);
				}

				newSMR.bones = newBones;
				if (sourceSMR.rootBone != null)
				{
					newSMR.rootBone = GetOrCreateBoneInTarget(sourceSMR.rootBone, sourceAnimator.transform, m_TargetRoot.transform, synchronizer);
				}
			}

			// --- Copy MeshRenderers ---
			foreach (var sourceMR in m_SelectedMRs)
			{
				if (sourceMR == null) continue;

				// Skip if the GameObject also has an SMR (rare but possible)
				if (sourceMR.GetComponent<SkinnedMeshRenderer>() != null) continue;

				Animator sourceAnimator = sourceMR.GetComponentInParent<Animator>();
				if (sourceAnimator == null) continue;

				// Replicate the path for the MR GameObject itself
				Transform newMRTransform = GetOrCreateBoneInTarget(sourceMR.transform, sourceAnimator.transform, m_TargetRoot.transform, synchronizer);
				
				// Add the MeshFilter and MeshRenderer components if they don't exist
				MeshFilter sourceFilter = sourceMR.GetComponent<MeshFilter>();
				if (sourceFilter != null)
				{
					MeshFilter newFilter = newMRTransform.gameObject.AddComponent<MeshFilter>();
					newFilter.sharedMesh = sourceFilter.sharedMesh;
					Undo.RegisterCompleteObjectUndo(newFilter, "Copy MeshFilter");

					MeshRenderer newMR = newMRTransform.gameObject.AddComponent<MeshRenderer>();
					newMR.sharedMaterials = sourceMR.sharedMaterials;
					Undo.RegisterCompleteObjectUndo(newMR, "Copy MeshRenderer");
				}
			}

			Undo.CollapseUndoOperations(groupIndex);
			EditorUtility.SetDirty(synchronizer);
			Debug.Log("SkinnedMeshCopier: Completed successfully with shared bone/transform hierarchy.");
		}

		private Transform GetOrCreateBoneInTarget(Transform sourceBone, Transform sourceRoot, Transform targetRoot, FPSBoneSynchronizer synchronizer)
		{
			List<Transform> path = new List<Transform>();
			Transform current = sourceBone;
			
			while (current != null && current != sourceRoot)
			{
				path.Insert(0, current);
				current = current.parent;
			}

			Transform currentTargetParent = targetRoot;
			foreach (var pathStep in path)
			{
				Transform existing = currentTargetParent.Find(pathStep.name);
				if (existing == null)
				{
					GameObject newBoneGO = new GameObject(pathStep.name);
					Undo.RegisterCreatedObjectUndo(newBoneGO, "Create Shared Node");
					existing = newBoneGO.transform;
					existing.SetParent(currentTargetParent);
					
					existing.localPosition = pathStep.localPosition;
					existing.localRotation = pathStep.localRotation;
					existing.localScale = pathStep.localScale;
				}

				if (synchronizer != null)
				{
					synchronizer.AddMapping(pathStep, existing);
				}

				currentTargetParent = existing;
			}

			return currentTargetParent;
		}
	}
}
