using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using static UnityEngine.GraphicsBuffer;

public class SceneFlowManager : MonoBehaviour
{
    private bool isSceneLoading = false;

    // sceneDic의 Key는 "TitleScene" "MainGameScene" "CutScene"로 고정하고 이걸로 로드할거임.
    // value는 실제로 빌드세팅에 들어간 씬들의 이름임. 씬 이름이 변해도 이렇게 딕셔너리로 조정하면 오류 안남
    Dictionary<SceneCategory, string> sceneDic;
    SceneCategory currentSceneCategory;

    private void Awake()
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        string[] sceneNames = new string[sceneCount];

        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i); // e.g. "Assets/Scenes/MainGameScene.unity"
            string name = Path.GetFileNameWithoutExtension(path);    // e.g. "MainGameScene"
            sceneNames[i] = name;
        }

        sceneDic = new Dictionary<SceneCategory, string>();

        foreach (var name in sceneNames)
        {
            if (name.Contains("Title")) sceneDic[SceneCategory.TitleScene] = name; 
            else if (name.Contains("MainGame")) sceneDic[SceneCategory.MainGameScene] = name;
            else if (name.Contains("Cut")) sceneDic[SceneCategory.CutScene] = name;
            else if (name.Contains("GameOver")) sceneDic[SceneCategory.GameOver] = name;
        }

        string currentSceneName = SceneManager.GetActiveScene().name;

        foreach (var pair in sceneDic)
            if (pair.Value == currentSceneName)
                currentSceneCategory = pair.Key;
    }

    private void Start()
    {
        SetAllCanvasSortingOrder();
    }

    public Coroutine LoadScene(SceneCategory sceneType, bool isStartGame = false)
    {
        currentSceneCategory = sceneType;

        return StartCoroutine(LoadSceneAsync(sceneType, isStartGame));
    }

    public Coroutine LoadGameOverScene(string gameOverCategoryStr)
    {
        ManagerObj.ScriptManager.EndScript();

        return StartCoroutine(LoadCoroutine(gameOverCategoryStr));

        IEnumerator LoadCoroutine(string gameOverCategoryStr)
        {
            GameOverCategory gameOverCategory = GameOverCategory.None;
            if (!Enum.TryParse<GameOverCategory>(gameOverCategoryStr, true, out gameOverCategory))
                Debug.LogWarning($"[ShowGameOverOveraly] gameOverCategoryStr를 GameOverCategory로 파싱 실패 : {gameOverCategoryStr}");

            ManagerObj.InGameProgressManager.GameOverSetting();
            yield return ManagerObj.SceneFlowManager.LoadScene(SceneCategory.GameOver);

            GameOverOverlay gameOverOverlay = FindFirstObjectByType<GameOverOverlay>();
            StartCoroutine(gameOverOverlay.SetOverlay(gameOverCategory));
        }
    }

    IEnumerator LoadSceneAsync(SceneCategory sceneType, bool isStartGame)
    {
        isSceneLoading = true;

        GameObject loadingBar = ManagerObj.PrefabLoader.GetPrefab(OverlaysPrefabCategory.LoadingBar, ManagerObj.DisplayManager.GlobalFadeImg.transform);

        Image img_Text = loadingBar.transform.Find("LoadingText").GetComponent<Image>(), img_Bar = loadingBar.transform.Find("LoadingBar").GetComponent<Image>();
        img_Text.color = new Color(1, 1, 1, 0);
        img_Bar.color = new Color(1, 1, 1, 0);

        if(ManagerObj.DisplayManager.GlobalFadeImg.color.a < 1)
            yield return ManagerObj.DisplayManager.GlobalFadeIn(1);

        ManagerObj.SoundManager.StopBGM();

        AsyncOperation operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneDic[sceneType]);
        operation.allowSceneActivation = true;

        float elapsedTimer = 0f;
        Coroutine fadeInLoadingTextVisual = null, fadeInLoadingBarVisual = null;
        while (!operation.isDone)
        {
            elapsedTimer += Time.deltaTime;

            // 1초 넘었을 때 로딩 UI 표시
            if (elapsedTimer >= 1f && (fadeInLoadingTextVisual ==  null && fadeInLoadingBarVisual == null))
            {
                fadeInLoadingTextVisual = StartCoroutine(FadeInImage(img_Text, 0.5f));
                fadeInLoadingBarVisual = StartCoroutine(FadeInImage(img_Bar, 0.5f));
            }
            yield return null;
        }

        float basicWaitingTimer = 1.5f;
        if (elapsedTimer < basicWaitingTimer)
            yield return new WaitForSeconds(basicWaitingTimer - elapsedTimer);

        switch (sceneType)
        {
            case SceneCategory.MainGameScene:
                if (isStartGame) // 만일 타이틀씬 -> 메인게임 으로 프롤로그 스킵 후 게임 시작한 경우
                {
                    ManagerObj.OptionManager.EnterInGame();
                    Destroy(loadingBar);
                    isSceneLoading = false;
                    yield break;
                }
                break;
            case SceneCategory.TitleScene:
                { // 타이틀 씬의 경우 CreatorLogo가 FadeOut을 처리해줌으로 이렇게 중단시킨다.
                    ManagerObj.OptionManager.ExitInGame();
                    yield return ManagerObj.Instance.InitInGameManagers(false); // 타이틀씬인 경우 IInGameManagers의 OutOfGame을 실행시켜준다.
                    Destroy(loadingBar);
                    isSceneLoading = false;
                    yield break;
                }
        }

        Destroy(loadingBar);

        yield return ManagerObj.DisplayManager.GlobalFadeOut(1);

        SetAllCanvasSortingOrder();

        isSceneLoading = false;

        IEnumerator FadeInImage(Image image, float duration)
        {
            float elapsedTime = 0f;
            float start = 0, target = 1f;
            Color color = image.color;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsedTime / duration));
                image.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }
            image.color = new Color(color.r, color.g, color.b, target);
        }
    }

    public string GetSceneName(SceneCategory sceneCategory)
    {
        return sceneDic[sceneCategory];
    }

    List<string> unsortedCanvasNames = new List<string> { "BookUI", "Cheat" };
    public void SetAllCanvasSortingOrder()
    {
        Canvas[] allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas canvas in allCanvases)
        {
            if (!unsortedCanvasNames.Contains(canvas.name))
                canvas.sortingOrder = ManagerObj.DisplayManager.GetSortingOrder(canvas.name);
        }
    }

    public SceneCategory CurrentCategory
    {
        get { return currentSceneCategory; }
    }

    public bool IsSceneLoading
    {
        get { return isSceneLoading; }
    }
}
