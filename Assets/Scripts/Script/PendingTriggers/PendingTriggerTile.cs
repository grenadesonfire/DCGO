using System;
using UnityEngine;
using UnityEngine.EventSystems;

//One pending effect inside a batch of the pending triggers panel
public class PendingTriggerTile : MonoBehaviour, IPointerClickHandler
{
    SkillInfo _skillInfo;
    Action<SkillInfo> _onRightClick;

    public void SetUp(SkillInfo skillInfo, Action<SkillInfo> onRightClick)
    {
        _skillInfo = skillInfo;
        _onRightClick = onRightClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            _onRightClick?.Invoke(_skillInfo);
        }
    }
}
