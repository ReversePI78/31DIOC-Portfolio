using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;

#if UNITY_EDITOR
using UnityEditor.PackageManager.UI;
#endif
using UnityEngine;

public class PrefabLoader : MonoBehaviour
{
    List<string> prefabspaths, spritePaths;

    void Awake()
    {
        prefabspaths = new List<string>();
        prefabspaths.Add("UIObjects/"); prefabspaths.Add("UIElementObjects/"); prefabspaths.Add("UIElementObjects/SpeechBubbles/");
        prefabspaths.Add("Windows/");

        prefabspaths.Add("NewProject/UICanvas/");
        prefabspaths.Add("NewProject/Characters/"); 
        prefabspaths.Add("NewProject/Overlays/"); 
        prefabspaths.Add("NewProject/Facilities/"); 
        prefabspaths.Add("NewProject/Elements/"); 

        spritePaths = new List<string>();
        spritePaths.Add("Character/");
        spritePaths.Add("Facility/");
        spritePaths.Add("Activity/"); 
        spritePaths.Add("InGameSprites/");
        spritePaths.Add("InGameSprites/SpeechBubble");
    }

    public GameObject GetPrefab(Enum prefabEnum, Transform parentTransform = null, Vector2? scale = null, Vector2? position = null)
    {
        GameObject loadedPrefab = LoadPrefab(prefabEnum.ToString());
        if (loadedPrefab == null) return null;

        GameObject obj = Instantiate(loadedPrefab, parentTransform);
        RemoveCloneSuffix(obj);
        ManagerObj.DisplayManager.ReplaceFonts(obj);

        obj.transform.localScale = scale ?? Vector2.one;
        obj.transform.localPosition = position ?? Vector2.zero;

        if (obj.GetComponent<Canvas>() != null)
            obj.GetComponent<Canvas>().sortingOrder = ManagerObj.DisplayManager.GetSortingOrder(obj);

        return obj;
    }

    GameObject LoadPrefab(string prefabName)
    {
        GameObject prefab;

        foreach (string path in prefabspaths)
        {
            string prefabPath = "Prefab/" + path + prefabName;
            prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                return prefab;
            }
        }

        // 프리팹을 찾지 못한 경우 null 반환
        return null;
    }

    public Sprite GetSprite(string spriteName)
    {
        Sprite sprite;

        foreach (string path in spritePaths)
        {
            string spritePath = "Sprite/" + path + spriteName;
            sprite = Resources.Load<Sprite>(spritePath);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    public void RemoveCloneSuffix(GameObject obj)
    {
        int index = obj.name.IndexOf("(Clone)");
        if (index > 0)
            obj.name = obj.name.Substring(0, index);
    }
}
