using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OptionManager : MonoBehaviour
{
    [SerializeField] bool startNewGame;
    [SerializeField] DifficultyCategory selectedDifficulty;

    bool isInGame;
    public bool IsInGame  => isInGame;

    public void EnterInGame()
    {
        isInGame = true;
        ManagerObj.DisplayManager.SetObjsWithDisableBeforeGameStartTagOnMainGame();

        ManagerObj.InGameProgressManager.SetStartScreen();
    }

    public void ExitInGame()
    {
        isInGame = false;
    }

    void Awake()
    {
        if (!PlayerPrefs.HasKey("IsFirstPlay") || GetPlayerSetting("IsFirstPlay") == 0) // 만일 처음 시작하는 경우 기본 세팅을 해줌
        {
            SetPlayerSetting("IsFirstPlay", 1);
            RestoreDefaultSettings();

            SetLanguageSetting("en"); // 이거 바꿔줘야함.
            // 스팀에서 현재 유저의 국가정보를 가져와서 "kr" 바꿔주기
        }

        StartCoroutine(SetApp()); 

        IEnumerator SetApp()
        {
            yield return InitApp();
#if UNITY_EDITOR
            if (ManagerObj.SceneFlowManager.CurrentCategory == SceneCategory.MainGameScene) // 유니티에디터에서 메인게임씬에서 실행할 경우
            {
                if (!ManagerObj.DataManager.IsSaveFileExists || startNewGame) // 만일 세이브 파일이 없거나 에디터상에서 새 게임 시작하기로 설정되었다면
                {
                    yield return LoadNewGameData(selectedDifficulty);
                } 
                else
                    yield return LoadSavedGameData();

                EnterInGame();
                ManagerObj.InGameProgressManager.DayTransition();
            }
#endif
        }
    }

    bool initAppCompleted;
    public bool InitAppCompleted => initAppCompleted;
    IEnumerator InitApp() // 프로그램 시작시 세팅, 최초 한 번만 실행
    {
        ManagerObj.DisplayManager.GlobalFadeIn(0);

        initAppCompleted = false;

        ManagerObj.DataManager.SetStaticDatas(); // etcTextFiles 설정
        while (!ManagerObj.DataManager.IsStaticDatasLoadedCompleted)
            yield return null;

        ManagerObj.DisplayManager.SetFont(GetLanguageSetting()); // 폰트 설정
        while (!ManagerObj.DisplayManager.IsFontLoadedCompleted)
            yield return null;

        initAppCompleted = true;
    }

    public IEnumerator LoadNewGameData(DifficultyCategory difficulty) // 게임 시작 시 세팅
    {
        StartCoroutine(ManagerObj.DataManager.LoadNewGameData(difficulty)); // RunTimeData가 모두 로드될때까지 대기, 만일 setNewGame가 false라면 difficulty와 상관 없이 저장되어 있는 데이터 로드
        while (!ManagerObj.DataManager.IsRuntimeDataLoadCompleted)
            yield return null;

        yield return SetupBeforeGameStart();
    }

    public IEnumerator LoadSavedGameData()
    {
        ManagerObj.DataManager.LoadSavedGameData();

        yield return SetupBeforeGameStart();
    }

    IEnumerator SetupBeforeGameStart()
    {
        yield return Resources.UnloadUnusedAssets(); // 리소스 에셋 정리
        yield return ManagerObj.Instance.InitInGameManagers(true); // IInGameManager 인터페이스를 가진 매니저 컴포넌트들을 InitInGame 해준다.
    }

    public void RestoreDefaultSettings() // 언어 제외 기본값 설정
    {
        SetPlayerSetting("WindowMode", 0);
        SetPlayerSetting("AspectRatio", 0);

        SetPlayerSetting("BGM", 6);
        SetPlayerSetting("SFX", 8);
        SetPlayerSetting("CharacterVoice", 8);

        SetPlayerSetting("SkipPrologue", 1);
    }

    public void SetPlayerSetting(string prefabKey, int value)
    {
        PlayerPrefs.SetInt(prefabKey, value);
    }

    public int GetPlayerSetting(string prefabKey)
    {
        return PlayerPrefs.GetInt(prefabKey);
    }

    public void SetLanguageSetting(string language)
    {
        PlayerPrefs.SetString("Language", language);
    }

    public string GetLanguageSetting()
    {
        return PlayerPrefs.GetString("Language");
    }
}
