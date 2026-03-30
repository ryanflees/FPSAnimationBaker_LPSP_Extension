using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using CR; 

public class WeaponPrefabGenerator : EditorWindow
{
    [MenuItem("Tools/CR/Generate Weapon Prefabs (Full Auto)")]
    public static void Generate()
    {
        GameObject rootObj = Selection.activeGameObject;
        if (rootObj == null)
        {
            Debug.LogError("请选中根节点 P_LPSP_FP_CH");
            return;
        }

        SourceFPSHands handsScript = rootObj.GetComponent<SourceFPSHands>();
        if (handsScript == null)
        {
            Debug.LogError("根节点未找到 SourceFPSHands 脚本！");
            return;
        }

        Transform inventory = FindChildRecursive(rootObj.transform, "P_LPSP_Inventory");
        if (inventory == null)
        {
            Debug.LogError("未找到 P_LPSP_Inventory 节点。");
            return;
        }

        string baseFolderPath = "Assets/GeneratedWeaponAssets";
        List<GameObject> weapons = new List<GameObject>();
        for (int i = 0; i < inventory.childCount; i++)
        {
            weapons.Add(inventory.GetChild(i).gameObject);
        }

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var currentWeapon in weapons)
            {
                foreach (var w in weapons)
                {
                    w.SetActive(w == currentWeapon);
                }

                UpdateWeaponRenderers(handsScript, currentWeapon);

                string weaponID = currentWeapon.name.Replace("P_LPSP_WEP_", "");
                string weaponFolderPath = Path.Combine(baseFolderPath, weaponID);
                if (!Directory.Exists(weaponFolderPath))
                {
                    Directory.CreateDirectory(weaponFolderPath);
                }

                string prefabName = $"{rootObj.name}_{weaponID}.prefab";
                string fullPrefabPath = Path.Combine(weaponFolderPath, prefabName);

                PrefabUtility.SaveAsPrefabAsset(rootObj, fullPrefabPath);
                
                Debug.Log($"生成成功: {weaponID}，关联了 {handsScript.m_WeaponRenderers.Count} 个渲染器");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
    }

    private static void UpdateWeaponRenderers(SourceFPSHands script, GameObject weaponRoot)
    {
        script.m_WeaponRenderers.Clear();

        Renderer[] allRenderers = weaponRoot.GetComponentsInChildren<Renderer>(true);

        script.m_WeaponRenderers.AddRange(allRenderers);

        EditorUtility.SetDirty(script);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
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
