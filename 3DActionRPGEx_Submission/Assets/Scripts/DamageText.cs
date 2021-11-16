using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamageText : MonoBehaviour
{
    [HideInInspector] public Text m_RefText = null;
    [HideInInspector] public float m_DamageVal = 0.0f;
    [HideInInspector] public Vector3 m_BaseWdPos = Vector3.zero;

    Animator m_RefAnimator = null;  //지금은 이게 " RootModel " 이기도 하다.

    RectTransform CanvasRect;
    Vector2 screenPos = Vector2.zero;
    Vector2 WdScPos = Vector2.zero;

    // Start is called before the first frame update
    void Start()
    {
        m_RefText = this.gameObject.GetComponentInChildren<Text>();
        if (m_RefText != null)
        {
            m_RefText.text = "-" + m_DamageVal.ToString() + " Dmg";
        }

        m_RefAnimator = GetComponentInChildren<Animator>();
        if (m_RefAnimator != null)
        {
            AnimatorStateInfo animaterStateInfo =
                m_RefAnimator.GetCurrentAnimatorStateInfo(0);   //첫번째 레이어

            //Debug.Log(animaterStateInfo.length);
            float a_LifeTime = animaterStateInfo.length;
            Destroy(gameObject, a_LifeTime);
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        CanvasRect = GameMgr.Inst.m_Damage_Canvas.GetComponent<RectTransform>();

        screenPos = Camera.main.WorldToViewportPoint(m_BaseWdPos);
        WdScPos.x = ((screenPos.x * CanvasRect.sizeDelta.x) -
                                        (CanvasRect.sizeDelta.x * 0.5f));
        WdScPos.y = ((screenPos.y * CanvasRect.sizeDelta.y) -
                                        (CanvasRect.sizeDelta.y * 0.5f));

        transform.GetComponent<RectTransform>().anchoredPosition = WdScPos;
    }
}
