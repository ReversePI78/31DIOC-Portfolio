using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SizeTransitionEffect : MonoBehaviour
{
    [SerializeField] float xDuration = 0.1f, yDuration = 0.05f, initialXScale = 0.05f, initialYScale = 0.05f, originalXScale, originalYScale; // 애니메이션 속도 조절을 위한 변수
    RectTransform rectTransform; // UI 이미지의 RectTransform 컴포넌트
    
    void Awake()
    {
        if(gameObject.GetComponent<CanvasGroup>() == null)
            gameObject.AddComponent<CanvasGroup>();

        rectTransform = GetComponent<RectTransform>(); // RectTransform 컴포넌트 참조 얻기
        originalXScale = rectTransform.localScale.x;
        originalYScale = rectTransform.localScale.y;
    }

    void OnEnable()
    {
        completeTransition = false;
        StartCoroutine(AdjustSizeTransition());
    }

    bool completeTransition;
    public bool CompleteTransition => completeTransition;
    IEnumerator AdjustSizeTransition()
    {
        completeTransition = false;

        gameObject.GetComponent<CanvasGroup>().alpha = 0f; // 전체 이미지 안보이게 해주고
        yield return new WaitForEndOfFrame(); // 한 프레임 쉬어주고
        gameObject.GetComponent<CanvasGroup>().alpha = 1f; // 다시 보이게 해줌

        // 가로로 커지는 애니메이션
        float elapsedTime = 0f;
        while (elapsedTime < xDuration)
        {
            float t = elapsedTime / xDuration;
            rectTransform.localScale = new Vector2(Mathf.Lerp(initialXScale, originalXScale, t), initialYScale);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        rectTransform.localScale = new Vector2(originalXScale, initialYScale); // 최종 크기 설정

        // 세로로 커지는 애니메이션
        elapsedTime = 0f;
        while (elapsedTime < yDuration)
        {
            float t = elapsedTime / yDuration;
            rectTransform.localScale = new Vector2(rectTransform.localScale.x, Mathf.Lerp(initialYScale, originalYScale, t));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        rectTransform.localScale = new Vector2(originalXScale, originalYScale); // 최종 크기 설정

        completeTransition = true;
    }
}