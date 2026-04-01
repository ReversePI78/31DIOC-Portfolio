using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextSetter : MonoBehaviour
{
    float originalFontSize;
    public TMP_Text TextArea => GetComponent<TMP_Text>();
    [SerializeField] string tag; // 바꾸면 안됨 다 초기화됨 ㅅㅂ

    private void Awake()
    {
        originalFontSize = TextArea.fontSize;
    }

    private void Start()
    {
        SetTextArea();
    }

    public void SetTextArea(string inputTag = "")
    {
        if (TextArea == null || !ManagerObj.DataManager.IsStaticDatasLoadedCompleted)
            return;

        if (string.IsNullOrEmpty(inputTag) && !string.IsNullOrEmpty(tag)) SetText(ManagerObj.DataManager.GetEtcText(tag));
        else if(!string.IsNullOrEmpty(inputTag)) SetText(ManagerObj.DataManager.GetEtcText(inputTag));
    }

    public string SetOverrideText
    {
        set
        {
            SetText(value);
        }
    }

    public string SetAdditionalText
    {
        set
        {
            SetText(TextArea.text += value);
        }
    }

    void SetText(string str)
    {
        if (originalFontSize == 0)
            originalFontSize = TextArea.fontSize;

        TextArea.text = str;
        TextArea.fontSize = originalFontSize;

        // ScrollRect의 Content에 ContentSizeFitter로 설정한 텍스트 오브젝트일 경우에는 AdjustFontSize를 진행하지 않는다.
        ScrollRect parentScrollRect = GetComponentInParent<ScrollRect>();
        if (parentScrollRect != null && parentScrollRect.content == this.transform)
            return;
        else
            AdjustFontSize(TextArea);
    }

    void AdjustFontSize(TMP_Text tmp_text)
    {
        float fontSize = tmp_text.fontSize;

        while (tmp_text.preferredHeight > tmp_text.rectTransform.rect.height)
        {
            fontSize -= 0.1f;
            tmp_text.fontSize = fontSize;
            tmp_text.ForceMeshUpdate();
        }
    }
}
