using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;  //SimpleJSON을 사용하기 위해 네임스페이스를 추가

public class LobbyMgr : MonoBehaviour
{
    public Text MyInfo_Text = null;
    public Text Ranking_Text = null;

    public Button m_GoStoreBtn = null;
    public Button m_GameStartBtn = null;
    public Button m_LogOutBtn = null;

    int m_My_Rank = 0;

    float RestoreTimer = 0.0f;
    public Button RestRk_Btn; //Restore Ranking Button

    float ShowMsTimer = 0.0f;
    public Text MessageText;

    float DelayGetLB = 3.0f;

    // Start is called before the first frame update
    void Start()
    {
        Ranking_Text.text = "";

        if (m_GoStoreBtn != null)
            m_GoStoreBtn.onClick.AddListener(() =>
            {

            });

        if (m_GameStartBtn != null)
            m_GameStartBtn.onClick.AddListener(() =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("scLobby");
            });

        if (m_LogOutBtn != null)
            m_LogOutBtn.onClick.AddListener(() =>
            {

            });

        DelayGetLB = 3.0f;  //3.0f초 뒤에 리더보드 한번 더 로딩하기..
    }//void Start()

    // Update is called once per frame
    void Update()
    {

    }//void Update()

}
