using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonInputEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private Image[] images;
    private TMP_Text[] texts;
    private Vector3 originalImageScale = Vector3.one;
    private Vector3 originalTextScale = Vector3.one;

    [SerializeField] float imageScale = 0.9f, tmp_textScale = 0.975f;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("Button component not found!");
            return;
        }

        // 자식 오브젝트들 중에서 Image와 TMP_Text 분리
        Transform[] children = GetComponentsInChildren<Transform>(true);
        var imageList = new System.Collections.Generic.List<Image>();
        var textList = new System.Collections.Generic.List<TMP_Text>();

        foreach (var t in children)
        {
            if (t == transform) continue; // 자기 자신은 제외

            var img = t.GetComponent<Image>();
            if (img != null) imageList.Add(img);

            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null) textList.Add(tmp);
        }

        images = imageList.ToArray();
        texts = textList.ToArray();

        // 원본 스케일 저장 (첫 번째 기준으로만)
        if (images.Length > 0)
            originalImageScale = images[0].transform.localScale;
        if (texts.Length > 0)
            originalTextScale = texts[0].transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable)
            return;

        foreach (var img in images)
        {
            img.transform.localScale = originalImageScale * imageScale;
        }

        foreach (var txt in texts)
        {
            txt.transform.localScale = originalTextScale * tmp_textScale;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        foreach (var img in images)
        {
            img.transform.localScale = originalImageScale;
        }

        foreach (var txt in texts)
        {
            txt.transform.localScale = originalTextScale;
        }
    }
}
