using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class GaugeBar : MonoBehaviour
{
    [Header("Refs")]
    Slider slider;
    [SerializeField] TextSetter valueTextSetter;          // 선택: "75/100" 등

    [Header("Values")]
    float realMax;           // 디버프 없는 진짜 최대
    float capMax;            // 디버프로 제한된 최대
    float current;           // 현재값

    void Reset() => slider = GetComponent<Slider>();

    void Awake()
    {
        if (!slider) slider = GetComponent<Slider>();

        // 읽기 전용
        slider.interactable = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.transition = Selectable.Transition.None; // 회색 비활성화 느낌 방지
    }

    public void SetGaugeBar(float currentValue, float maxValue, float blockValue, string additionalTextSetterKey = "")
    {
        GaugeBarBasicSetting(maxValue, blockValue);

        current = Mathf.Clamp(currentValue, 0f, capMax);
        slider.SetValueWithoutNotify(current);

        SetTextLabel(additionalTextSetterKey);
    }

    public void SetGaugeBarWithCoroutine(float baseValue, float addValue, float maxValue, float blockValue = 0, string additionalTextSetterKey = "")
    {
        GaugeBarBasicSetting(maxValue, blockValue);
        SetTextLabel(additionalTextSetterKey);

        StartCoroutine(SetGaugeBar(baseValue, addValue, additionalTextSetterKey));

        IEnumerator SetGaugeBar(float baseValue, float addValue, string additionalTextSetterKey)
        {
            slider.SetValueWithoutNotify(baseValue);  // 1) 기준값으로 세팅(캡 한도 안으로 자동 클램프)
            if (addValue > 0f && baseValue >= maxValue)
            {
                current = baseValue;
                SetTextLabel(additionalTextSetterKey);
                yield break;
            }

            float target = Mathf.Clamp(baseValue + addValue, 0f, capMax); // 2) 목표값 계산 (base + delta), cap 범위로 클램프
            float elapsed = 0f, duration = 1.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 선형 보간
                current = Mathf.Lerp(baseValue, target, t);
                slider.SetValueWithoutNotify(current);
                SetTextLabel(additionalTextSetterKey);
                yield return null;
            }

            slider.SetValueWithoutNotify(target);  // 마지막으로 정확히 맞춰둠
            SetTextLabel(additionalTextSetterKey);
        }
    }

    void GaugeBarBasicSetting(float maxValue, float blockValue)
    {
        realMax = Mathf.Max(0.0001f, maxValue);
        slider.maxValue = realMax;

        capMax = Mathf.Clamp(realMax - blockValue, 0f, realMax);
        if (blockValue > 0) ApplyGaugeBlock(); // 만일 blockValue가 지정된 경우, 블락커 오브젝트를 로드함
    }

    void ApplyGaugeBlock()
    {
        float blockedRatio = Mathf.Clamp01((realMax - capMax) / realMax);

        Image capOverlay = ManagerObj.PrefabLoader.GetPrefab(ElementsPrefabCategory.GaugeBarBlocker, slider.fillRect.transform.parent).GetComponent<Image>();
        capOverlay.transform.SetAsFirstSibling();

        // fillArea에 딱 맞추기
        RectTransform fillAreaRT = slider.fillRect.transform.parent as RectTransform;
        RectTransform overlayRT = capOverlay.rectTransform;

        // 부모를 확실히 fillArea로 고정(로컬 기준 유지)
        overlayRT.SetParent(fillAreaRT, false);

        // fillArea 전체를 덮도록 스트레치
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.pivot = fillAreaRT.pivot;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;
        overlayRT.anchoredPosition = Vector2.zero;
        overlayRT.localRotation = Quaternion.identity;
        overlayRT.localScale = Vector3.one;

        capOverlay.type = Image.Type.Filled;
        capOverlay.fillMethod = Image.FillMethod.Horizontal;
        capOverlay.fillOrigin = (int)Image.OriginHorizontal.Right; // 오른쪽에서 채움
        capOverlay.fillAmount = blockedRatio;
        capOverlay.raycastTarget = false;

        ApplyMarker(capOverlay.transform.GetChild(0) as RectTransform);

        void ApplyMarker(RectTransform capMarker)
        {
            float u = Mathf.Approximately(realMax, 0f) ? 0f : Mathf.Clamp01(capMax / realMax); // usable(열린) 비율
            float b = 1f - u; // blocked(막힌) 비율

            // 막힌 구간의 중앙 x 위치
            float x = u + b * 0.5f;

            // capOverlay가 fillArea에 맞춰진 뒤의 높이 기준으로 사이즈 결정
            RectTransform overlayRT = capOverlay.rectTransform;
            float h = overlayRT.rect.height * 0.85f; // height = capOverlay.height * 0.8
            float w = h;                             // width = height

            // 아이콘을 점(정사각)으로 중앙 배치
            capMarker.anchorMin = new Vector2(x, 0.5f);
            capMarker.anchorMax = new Vector2(x, 0.5f);
            capMarker.pivot = new Vector2(0.5f, 0.5f);
            capMarker.sizeDelta = new Vector2(w, h);
            capMarker.anchoredPosition = Vector2.zero;
        }
    }

    void SetTextLabel(string additionalTextSetterKey)
    {
        if (!valueTextSetter) 
            return;

        string valueStr = $"({FormatValue(current)}/{FormatValue(capMax)})";
        if (valueTextSetter.TextArea.text.Contains(valueStr)) return;

        valueTextSetter.TextArea.text = "";
        if (additionalTextSetterKey != "")
        {
            valueTextSetter.SetTextArea(additionalTextSetterKey);
            valueTextSetter.SetAdditionalText = " ";
        }

        valueTextSetter.SetAdditionalText = $"({FormatValue(current)}/{FormatValue(capMax)})";

        string FormatValue(float value)
        {
            // 소수점 이하가 0인지 검사
            if (Mathf.Approximately(value % 1f, 0f))
                return ((int)value).ToString(); // 정수로 출력
            else
                return value.ToString("0.0"); // 소수점 첫째 자리까지
        }
    }
}
