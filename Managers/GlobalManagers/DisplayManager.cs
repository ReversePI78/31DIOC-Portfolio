using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;
using System.Xml.Linq;


#if UNITY_EDITOR
using UnityEditor.PackageManager.UI;
#endif

public class DisplayManager : MonoBehaviour
{
    TMP_FontAsset mainFont, bookFont;

    void Awake()
    {
        globalFadeImg.gameObject.SetActive(false);
        globalFadeImg.GetComponent<Canvas>().sortingOrder = GetSortingOrder(globalFadeImg.gameObject);

        showLogo = true;
    }

    private void OnEnable()
    {
        // 씬 로드 이벤트에 함수 등록
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 씬이 로드될 때 호출되는 함수
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReplaceFonts();
        SetObjsWithDisableBeforeGameStartTagOnMainGame();

        ManagerObj.SceneFlowManager.SetAllCanvasSortingOrder();

        if (ManagerObj.SceneFlowManager.CurrentCategory == SceneCategory.TitleScene)
            ManagerObj.DisplayManager.CreatorLogoFadeInOut();
    }

    public Coroutine GlobalFadeIn(float fadeInOutTimer)
    {
        return StartCoroutine(FadeImage(true, fadeInOutTimer));
    }

    public Coroutine GlobalFadeOut(float fadeInOutTimer)
    {
        return StartCoroutine(FadeImage(false, fadeInOutTimer));
    }

    [SerializeField] Image globalFadeImg;
    public Image GlobalFadeImg { get { return globalFadeImg; } }
    IEnumerator FadeImage(bool fadeIn, float fadeInOutTimer, bool isFadeSkippable = false)
    {
        if (fadeIn)
            globalFadeImg.gameObject.SetActive(true);
        else
        {
            if (!globalFadeImg.gameObject.activeSelf)
            {
                Debug.LogError("FadeImage : FadeOut이 실행되었으나 globalFadeImg가 비활성화되어있음");
                yield break;
            }

            foreach (Transform child in globalFadeImg.transform)
                Destroy(child.gameObject);
        }

        float elapsed = 0f, startValue = fadeIn ? 0 : 1, targetValue = fadeIn ? 1 : 0;

        while (elapsed < fadeInOutTimer)
        {
            if (isFadeSkippable && Input.GetMouseButtonDown(0)) // 왼쪽 마우스 클릭 감지
            {
                break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInOutTimer);
            float a = Mathf.Lerp(startValue, targetValue, t);
            globalFadeImg.color = new Color(1, 1, 1, a);
            yield return null;
        }

        globalFadeImg.color = new Color(1, 1, 1, targetValue);

        if (!fadeIn)
            globalFadeImg.gameObject.SetActive(false); // 비활성화인 경우 globalFadeImg를 비활성화한다.
    }

    public void MoveScrollbarInScrollView(ScrollRect scrollView, bool isTop = true)
    {
        float verticalPos = isTop ? 1f : 0f;
        scrollView.normalizedPosition = new Vector2(scrollView.normalizedPosition.x, verticalPos);
    }


    private readonly Queue<WindowInfo> windowQueue = new Queue<WindowInfo>();
    public static GameObject CurrentWindow { get; set; }
    public bool HasPendingWindows { get => CurrentWindow != null || windowQueue.Count > 0; } // 윈도우를 실행중인지 여부를 나타내는 프로퍼티
    private bool isProcessingQueue = false;
    public void ShowWindow(WindowInfo windowInfo)
    {
        if (CurrentWindow != null) // 창이 있으면 큐에 넣고 코루틴만 보증
        {
            if (!windowQueue.Contains(windowInfo)) // 중복 윈도우가 들어왔는지 확인. 만일 중복 윈도우라면 enqueue 안함
                windowQueue.Enqueue(windowInfo);

            if (!isProcessingQueue) // isProcessingQueue가 true면 ProcessWindowQueue()를 진행하지 않고 windowQueue에 값만 넣고 함수 종료
                StartCoroutine(ProcessWindowQueue());
            return;
        }

        BookUI.Instance.ActiveClosedBook();

        CurrentWindow = ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.Window);

        if (globalFadeImg.gameObject.activeSelf)
            CurrentWindow.GetComponent<Canvas>().sortingOrder = GetSortingOrder(globalFadeImg.gameObject) + 1;
        else
            CurrentWindow.GetComponent<Canvas>().sortingOrder = GetSortingOrder(CurrentWindow);

        Window_Text enabledWindow = null;

        foreach (Window_Text currentWindow in CurrentWindow.GetComponentsInChildren<Window_Text>(true))
        {
            currentWindow.gameObject.SetActive(false);

            if ((!windowInfo.IsObjWindow && currentWindow is Window_Object) || (windowInfo.IsObjWindow && currentWindow is not Window_Object))
                continue;

            enabledWindow = currentWindow;
        }

        enabledWindow.gameObject.SetActive(true);
        enabledWindow.GetComponent<Window_Text>().SetWindow(windowInfo); // 만일 Window_Obj인 경우, 오버라이딩한 SetWindow가 실행된다.
    }

    private IEnumerator ProcessWindowQueue()
    {
        isProcessingQueue = true;

        while (true)
        {
            while (CurrentWindow != null) // 현재 창 닫힐 때까지 대기
                yield return null;

            if (windowQueue.Count == 0) break;

            var next = windowQueue.Dequeue();
            
            ShowWindow(next); // 다음 요청 실행 (현재는 CurrentWindow == null 상태이므로 바로 표시)

            yield return null; // 예방적 한 프레임 양보
        }

        isProcessingQueue = false;
    }

    public void InitWindow()
    {
        windowQueue.Clear();
        Destroy(CurrentWindow);
        isProcessingQueue = false;
    }

    public int GetSortingOrder(GameObject gameObject)
    {
        return GetSortingOrder(gameObject.name);
    }

    readonly Dictionary<SortingOrderCategory, int> sortingOrderDict = new() // 밑에 GetSortingOrder에 쓰임
    {
        { SortingOrderCategory.ActivityButtonPanel, -1 },
        { SortingOrderCategory.ChoicePaper, 1 },
        { SortingOrderCategory.CutSceneDialogueViewer, 0 },
        { SortingOrderCategory.ESCMenu, 9 },
        { SortingOrderCategory.FacilitySurvey, -2 },
        { SortingOrderCategory.InteractorObjs, -1 }, // 별도 설정
        { SortingOrderCategory.InventoryEditor, 3 },
        { SortingOrderCategory.ItemExchangeEditor, 3 },
        { SortingOrderCategory.MainGameSceneDialogueViewer, 0 },
        { SortingOrderCategory.Window, 8 },
        { SortingOrderCategory.Settings, 9 },
        { SortingOrderCategory.PlayGuide, 9 },
        { SortingOrderCategory.GameOverOverlay, 9 },

        { SortingOrderCategory.DialogueLog, 2 },
        { SortingOrderCategory.MessageBoard, 0 },
        { SortingOrderCategory.UpdatedInfoPanel, 0 },
        { SortingOrderCategory.LifeStatGaugePanel, 1 },

        { SortingOrderCategory.TitleSceneUI, 0 },

        { SortingOrderCategory.GlobalFadeImage, 10 },
        { SortingOrderCategory.ClosedBook, -1 },
        { SortingOrderCategory.OpenedBook, 2 },

        { SortingOrderCategory.CharacterController_Body, 1 },
        { SortingOrderCategory.CharacterController_Expression, 2 },
        { SortingOrderCategory.CharacterController_SpecialEffect, 3 },
        { SortingOrderCategory.CharacterController_Shadow, 5 }, 
        // CharacterController_SpecialEffect(3) + 2 → 5

        { SortingOrderCategory.FacilityObj, -1 },
        { SortingOrderCategory.FacilityFade, 0 } // 상황에 따라 변경
    };
    [SerializeField] List<Canvas> notCheckGetSortingOrderObjs; // 이 캔버스들은 체크 안함.
    public int GetSortingOrder(string objName)
    {
        if(notCheckGetSortingOrderObjs.Any(obj => obj.name == objName))
            return 10;

        SortingOrderCategory[] sortingOrderCategories = (SortingOrderCategory[])Enum.GetValues(typeof(SortingOrderCategory));

        foreach (SortingOrderCategory category in sortingOrderCategories)
        {
            if (Enum.TryParse<SortingOrderCategory>(objName, true, out var result))
            {
                return sortingOrderDict[result];
            }
        }

        Debug.LogError($"DisplayManager : GetSortingOrder 중 SortingOrderCategory[] sortingOrderCategories에서 매개변수 objName와 같은 이름의 enum이 없습니다.(-1 반환) objName : {objName}");
        return -1;
    }

    public void SetObjsWithDisableBeforeGameStartTagOnMainGame()
    {
        if (ManagerObj.SceneFlowManager.CurrentCategory != SceneCategory.MainGameScene)
            return;

        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.CompareTag(GameObjectTagCategory.DisableBeforeGameStart.ToString()))
                {
                    child.gameObject.SetActive(ManagerObj.OptionManager.IsInGame);
                }
            }
        }
    }

    public void ReplaceFonts(GameObject target = null) // target이 null이면 씬의 모든 오브젝트, 매개변수가 있으면 해당 오브젝트의 폰트를 바꿈
    {
        if (target == null)
        {
            // 씬 전체 루트 오브젝트 순회
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                ReplaceFontsRecursively(root.transform);
            }
        }
        else
        {
            // target 자신의 자식 오브젝트만 순회
            foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
            {
                ReplaceFontsRecursively(child);
            }
        }

        void ReplaceFontsRecursively(Transform target)
        {
            TMP_Text tmp_Text = target.GetComponent<TMP_Text>();
            if (tmp_Text != null)
            {
                if (target.CompareTag(GameObjectTagCategory.StaticText.ToString()))
                    return;
                else if (target.CompareTag(GameObjectTagCategory.BookText.ToString()))
                    tmp_Text.font = bookFont;
                else
                    tmp_Text.font = mainFont;
            }

            // 자식들도 재귀적으로 순회
            foreach (Transform child in target)
            {
                ReplaceFontsRecursively(child);
            }
        }
    }

    bool isFontLoadedCompleted;
    public bool IsFontLoadedCompleted => isFontLoadedCompleted;
    public void SetFont(string language)
    {
        string mainLabel = $"Font_{language}";
        string bookLabel = $"Font_{language}_book";

        isFontLoadedCompleted = false;
        var mainHandle = Addressables.LoadAssetAsync<TMP_FontAsset>(mainLabel);
        mainHandle.Completed += mh =>
        {
            if (mh.Status == AsyncOperationStatus.Succeeded)
            {
                mainFont = mh.Result;
                ReplaceFonts(); // 메인 폰트는 로드가 완료되면 모두 해당 폰트로 바꿔준다.
            }
            else Debug.Log($"MainFont 로드 실패 string language + {language}");

            isFontLoadedCompleted = true;
        };

        var bookHandle = Addressables.LoadAssetAsync<TMP_FontAsset>(bookLabel);
        Addressables.LoadAssetAsync<TMP_FontAsset>(bookLabel).Completed += bh =>
        {
            if (bh.Status == AsyncOperationStatus.Succeeded)
                bookFont = bh.Result;
            else Debug.Log($"BookFont 로드 실패 string language + {language}");
        };

        // 폰트 세팅이 끝나면 모든 TextSetter들을 다시 실행시켜준다.
        foreach (GameObject obj in SceneManager.GetActiveScene().GetRootGameObjects())
            ApplyTextSetterRecursively(obj.transform); // 만일 EtcTextDatas를 처음 로드한 경우, 현재 씬에 있는 모든 TextSetter들을 세팅해준다.

        void ApplyTextSetterRecursively(Transform target)
        {
            TextSetter textSetter = target.GetComponent<TextSetter>();
            if (textSetter != null)
            {
                textSetter.SetTextArea();
            }

            // 자식들도 재귀적으로 순회
            foreach (Transform child in target)
            {
                ApplyTextSetterRecursively(child);
            }
        }
    }

    [SerializeField] float beforeAfterTimer = 1f, fadeInOutTimer = 0.25f, showLogoTimer = 2.5f;
    bool showLogo;
    public bool ShwoLogo { get => showLogo; set => showLogo = value; } // 이걸 false로 설정하면서 로고를 보여줄지 말지를 설정한다.
    public void CreatorLogoFadeInOut()
    {
        GlobalFadeIn(0); // 글로벌 페이드 인 실행

        StartCoroutine(ActiveCreatorLogo());

        IEnumerator ActiveCreatorLogo()
        {
            yield return new WaitForSecondsRealtime(beforeAfterTimer); // 로고 나오기 전

            if (showLogo)
            {
                GameObject creatorLogo = ManagerObj.PrefabLoader.GetPrefab(OverlaysPrefabCategory.CreatorLogo, globalFadeImg.transform);

                Image logoImg = creatorLogo.GetComponent<Image>(); // 로고가 항상 배경보다 앞에 잇으므로 GetChild(1)
                logoImg.color = new Color(1, 1, 1, 0);

                while (!ManagerObj.OptionManager.InitAppCompleted)
                    yield return null;

                Coroutine LogoFadeInOutCoroutine = StartCoroutine(LogoFadeInOut(logoImg));

                float logoTimer = 0f;
                while (logoTimer < fadeInOutTimer * 2f + showLogoTimer)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        StopCoroutine(LogoFadeInOutCoroutine);
                        logoImg.color = new Color(1, 1, 1, 0);
                        break;
                    }
                    else
                    {
                        logoTimer += Time.deltaTime;
                        yield return null;
                    }
                }

                yield return new WaitForSeconds(beforeAfterTimer); // 로고 들어간 후

                Destroy(creatorLogo);
            }
            else
            {
                showLogo = true;
            }

            while (!isFontLoadedCompleted) // 폰트 세팅이 안되어있다면 대기
            {
                yield return null;
            }

            yield return GlobalFadeOut(fadeInOutTimer);

            ManagerObj.SoundManager.PlayBGM(7);
        }

        IEnumerator LogoFadeInOut(Image logoImg)
        {
            float elapsed = 0f;
            while (logoImg.color.a < 1)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(0, 1, elapsed / fadeInOutTimer);
                logoImg.color = new Color(1, 1, 1, a);
                yield return null;
            }
            logoImg.color = new Color(1, 1, 1, 1);

            yield return new WaitForSecondsRealtime(showLogoTimer);

            elapsed = 0f;
            while (logoImg.color.a > 0)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(1, 0, elapsed / fadeInOutTimer);
                logoImg.color = new Color(1, 1, 1, a);
                yield return null;
            }
            logoImg.color = new Color(1, 1, 1, 0);
        }
    }
}
