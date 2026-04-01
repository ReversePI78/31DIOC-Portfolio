using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SFXSetter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler
{
    [SerializeField] string SFX_Start, SFX_Enable, SFX_Enter, SFX_Exit, SFX_PointerClick, SFX_PointerDown, SFX_PointerUp, SFX_Select;

    public void PlaySFX(string sfxID)
    {
        if (GetComponent<UnityEngine.UI.Button>() != null && !GetComponent<UnityEngine.UI.Button>().interactable) // 만일 버튼 오브젝트에 있는 setter의 경우, 버튼의 interactable이 false인 경우 소리를 안나오게 해줌
            return;

        ManagerObj.SoundManager.PlaySFX(sfxID);
    }

    private void Start()
    {
        PlaySFX(SFX_Start);
    }

    private void OnEnable()
    {
        PlaySFX(SFX_Enable);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlaySFX(SFX_Enter);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PlaySFX(SFX_Exit);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlaySFX(SFX_PointerClick);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        PlaySFX(SFX_PointerDown);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        PlaySFX(SFX_PointerUp);
    }

    public void OnSelect(BaseEventData eventData)
    {
        PlaySFX(SFX_Select);
    }
}
