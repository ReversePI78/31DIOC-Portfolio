using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SoundManager : MonoBehaviour
{
    [SerializeField] AudioSource BGM, SFX, characterVoice;

    private void OnEnable()
    {
        SetBGMVolume();
    }

    private void Start()
    {
    }

    public int CurrentBGMTrackNum { get; set; }
    AsyncOperationHandle<AudioClip> currentBGMHandle; // 현재 로드된 BGM 핸들
    Coroutine stopBGMCoroutine, playBGMCoroutine;
    float bgmFadeTimer = 0.5f, waitTimerStopToPlay = 0.3f;
    public void PlayBGM(int trackNum)
    {
        StopAllCoroutines();

        if (trackNum < 0)
        {
            StopBGM();
            return;
        }

        string bgmName = "BGM_";
        if (trackNum < 10) bgmName += "0" + trackNum;
        else bgmName += "" + trackNum;

        if (BGM.clip != null && BGM.clip.name == bgmName && BGM.isPlaying) // 만약 현재 재생 중인 클립이 있고, 해당 클립의 이름이 같다면 종료
        {
            SetBGMVolume();
            return;
        }

        if (playBGMCoroutine != null)
        {
            StopCoroutine(playBGMCoroutine);
            playBGMCoroutine = null;

            if (stopBGMCoroutine != null)
            {
                StopCoroutine(stopBGMCoroutine);
                stopBGMCoroutine = null;
            }
        }

        SetBGMVolume();

        CurrentBGMTrackNum = trackNum;
        playBGMCoroutine = StartCoroutine(Play(bgmName)); // 새로운 트랙 로드 (비동기 처리)

        IEnumerator Play(string bgmName)
        {
            if (BGM.clip != null)
            {
                if (stopBGMCoroutine != null)
                {
                    StopCoroutine(stopBGMCoroutine);
                    stopBGMCoroutine = null;
                }

                StopBGM();
                yield return new WaitForSeconds(bgmFadeTimer + waitTimerStopToPlay);
            }

            var handle = Addressables.LoadAssetAsync<AudioClip>(bgmName); // 비동기로 BGM 로드
            yield return handle; // 로드가 완료될 때까지 기다림

            if (handle.Status == AsyncOperationStatus.Succeeded) // 로드가 완료되면 새로운 트랙을 재생
            {
                if (currentBGMHandle.IsValid()) // 만약 이전 트랙이 있으면 언로드
                    Addressables.Release(currentBGMHandle);

                BGM.clip = handle.Result;
                BGM.Play();
                currentBGMHandle = handle; // 현재 핸들 저장

                float fadeDuration = bgmFadeTimer * (BGM.volume / ManagerObj.OptionManager.GetPlayerSetting("BGM") * 0.05f), elapsed = 0f, startVolume = BGM.volume;
                while (BGM.volume < ManagerObj.OptionManager.GetPlayerSetting("BGM") * 0.05f)
                {
                    elapsed += Time.deltaTime;
                    BGM.volume = Mathf.Lerp(startVolume, ManagerObj.OptionManager.GetPlayerSetting("BGM") * 0.05f, elapsed / fadeDuration);
                    yield return null;
                }
                BGM.volume = ManagerObj.OptionManager.GetPlayerSetting("BGM") * 0.05f;
            }
            else
            {
                Debug.LogError("BGM 로드 실패: " + bgmName);
            }

            playBGMCoroutine = null;
        }
    }

    public void StopBGM()
    {
        StopAllCoroutines();
        ManagerObj.SoundManager.CurrentBGMTrackNum = -1;
        stopBGMCoroutine = StartCoroutine(Stop());

        IEnumerator Stop()
        {
            float fadeDuration = bgmFadeTimer * (BGM.volume / ManagerObj.OptionManager.GetPlayerSetting("BGM") * 0.05f);

            if (BGM.isPlaying && BGM.volume > 0)
            {
                float elapsed = 0f, startVolume = BGM.volume;
                while (BGM.volume > 0)
                {
                    elapsed += Time.deltaTime;
                    BGM.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
                    yield return null;
                }

                BGM.Stop();
                BGM.clip = null;
            }

            stopBGMCoroutine = null;
        }
    }

    public void SetBGMVolume()
    {
        BGM.GetComponent<AudioSource>().volume = ManagerObj.OptionManager.GetPlayerSetting("BGM") * 0.05f;
    }

    public void PlaySFX(string SFXID)
    {
        AudioClip clip = Resources.Load($"Sound/SFX/{SFXID}") as AudioClip;
        if (clip == null) return;

        AudioSource effect = SFX.AddComponent<AudioSource>();
        effect.clip = clip;
        effect.volume = ManagerObj.OptionManager.GetPlayerSetting("SFX") * 0.05f;

        StartCoroutine(RemoveAfterPlay(effect));

        IEnumerator RemoveAfterPlay(AudioSource effectAudio)
        {
            effect.Play();
            yield return new WaitUntil(() => !effectAudio.isPlaying);
            Destroy(effectAudio); // 컴포넌트만 제거
        }
    }

    public void PlayCharacterVoice(string voiceID)
    {
        characterVoice.volume = ManagerObj.OptionManager.GetPlayerSetting("CharacterVoice") * 0.05f;

        characterVoice.clip = Resources.Load($"Sound/CharacterVoice/{voiceID}") as AudioClip;

        if (characterVoice.clip != null)
            characterVoice.Play();
    }

    public void StopCharacterVoice()
    {
        characterVoice.GetComponent<AudioSource>().Stop();
    }
}
