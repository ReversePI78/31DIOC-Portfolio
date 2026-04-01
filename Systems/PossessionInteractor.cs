using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public abstract class PossessionInteractor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static Possession HoverPossession { get; set; }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (possessionLayer == null)
        {
            Debug.LogError("PossessionLayer를 먼저 설정해주세요.");
            return;
        }

        HoverPossession = possessionLayer.PossessionData;
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        ClearHover();
    }

    protected virtual void ClearHover()
    {
        HoverPossession = null;
    }

    public virtual void SetInteractionTransform(ref GameObject staticTarget, GameObject interactorPrefab) // 여기서 싱글톤 설정해주고, 상속 받는 쪽에서 크기,위치 조정
    {
        if (staticTarget != null)
        {
            Destroy(staticTarget);
        }

        staticTarget = Instantiate(interactorPrefab, interactorPrefab.transform.parent);
    }

    public abstract void SetStaticObjectPosition(Transform objTransform); // 인터렉션 종류(하이라이트, 설명 버블 등)에 따라서 위치를 지정해주는 함수. 밑의 FollowPosition과 같이 쓰임

    public IEnumerator FollowPosition(GameObject staticObject, Action clearAction) // (static)Interaction 오브젝트가 현재 오브젝트를 따라오도록 하는 코루틴. 만일 현재 오브젝트가 다른 오브젝트에 의해 조금이라도 가려진다면 clearAction을 실행한다.
    {
        Transform objTransform = staticObject.transform;

        while (staticObject != null)
        {
            SetStaticObjectPosition(objTransform);
            if (IsRaycastBlocked) // 만일 다른 오브젝트에 가려져 있다면 
            {
                clearAction();
                yield break;
            }

            yield return null;
        }
    }

    bool IsRaycastBlocked // 이미지가 다른 오브젝트에 가려져있는지 판단하는 프로퍼티, 90% 이하 만큼 가려지면 true를 반환한다.
    {
        get
        {
            Image image = GetComponent<Image>();
            RectTransform rt = image.rectTransform;

            Canvas canvas = image.canvas;
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            // 코너와 센터 계산
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;

            const float t = 0.9f; // 0~1 사이. 1이면 코너, 0.9면 코너에서 10% 안쪽

            for (int i = 0; i < 4; i++)
            {
                Vector3 innerPoint = Vector3.Lerp(center, corners[i], t);
                Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(cam, innerPoint);

                var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                {
                    position = screenPt
                };
                var results = new List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, results);

                // 이 지점의 최상단이 자기 자신이 아니면 가려진 것으로 간주
                if (results.Count == 0 || results[0].gameObject != image.gameObject)
                    return true;
            }

            // 네 지점 모두 자기 자신이 최상단일 때만 가려지지 않은 것으로 판단
            return false;
        }
    }

    public Transform AdjustSizeBySlotRatio(GameObject staticTarget)
    {
        Vector2 calculatedRatio = Vector2.one;
        PossessionSlot currentSlot = GetComponentInParent<PossessionSlot>(); // 슬롯 컴포넌트를 찾아서 현재 PossessionLayer의 비율 알아오기
        if (currentSlot == null)
            Debug.LogError("PossessionDescriptionInteractor : calculatedRatio를 설정 중에 부모 오브젝트 중에 PossessionSlot가 없음. calculatedRatio는 Vector2.one으로 설정");
        else
            calculatedRatio = currentSlot.CalculatedRatio;

        staticTarget.GetComponent<RectTransform>().sizeDelta = staticTarget.GetComponent<RectTransform>().sizeDelta * calculatedRatio; // 비율에 맞춰서 Interactor 크기 조정해주기

        Transform contentObj = staticTarget.transform.Find("Content");
        if(contentObj == null)
        {
            Debug.LogError($"PossessionInteractor : AdjustSizeBySlotRatio 도중 자식 오브젝트에 LocalScale을 변경할 Content가 없습니다. 매개변수로 받은 staticTarget 이름 : {staticTarget.name}");
            return null;
        }

        contentObj.localScale = calculatedRatio; // 자식오브젝트 Content의 Scale 변경
        return contentObj;
    }

    public static void AddInteractorOnPossessionImages<T>(Transform target) where T : PossessionInteractor // 스태틱 함수. 원하는 인터렉션 종류로 PossessionImage에 AddComponenet해주면 됨
    {
        Transform[] children = target.GetComponentsInChildren<Transform>(true);
        foreach (Transform chlid in children)
        {
            PossessionLayer possessionLayer = chlid.GetComponent<PossessionLayer>();
            if (possessionLayer != null && possessionLayer.PossessionImage.GetComponent<T>() == null)
            {
                T Interactor = possessionLayer.PossessionImage.AddComponent<T>();
                Interactor.PossessionLayer = possessionLayer;
            }
        }
    }

    PossessionLayer possessionLayer;
    protected PossessionLayer PossessionLayer
    {
        get => possessionLayer;
        set => possessionLayer = value;
    }

    public static InteractorObjs InteractorObjs { get; set; }
    protected static HashSet<GameObject> AliveObjects = new HashSet<GameObject>();
    protected virtual void Awake()
    {
        AliveObjects.Add(gameObject);

        if (InteractorObjs == null) // ItemHighlight UI 오브젝트가 없으면 생성
        {
            InteractorObjs = ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.InteractorObjs).GetComponent<InteractorObjs>();
            InteractorObjs.GetComponent<Canvas>().sortingOrder = GetComponentInParent<Canvas>().sortingOrder + 1; // 현재 이미지보다 sortingOrder가 1 더 크게 설정

            foreach (Transform child in InteractorObjs.transform)
                child.gameObject.SetActive(false);
        }
    }

    protected virtual void OnDestroy() // AliveObjects.Count가 0일때 자식 오브젝트에서 인터렉션 오브젝트 파괴하도록 구현
    {
        AliveObjects.Remove(gameObject);

        if (AliveObjects.Count == 0) // AliveObjects.Count이 0이 되면 인터렉션 오브젝트 제거
        {
            Destroy(InteractorObjs.gameObject);
        }
    }
}
