using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

enum JoyStickType
{
    Fixed = 0,      //m_JoySBackObj.activeSelf == true && m_JoystickPickPanel.activeSelf == false
    Flexible = 1,   //m_JoySBackObj.activeSelf == true && m_JoystickPickPanel.activeSelf == true
    FlexibleOnOff = 2 //m_JoySBackObj.activeSelf == false && m_JoystickPickPanel.activeSelf == true
}

public class GameMgr : MonoBehaviourPunCallbacks, IPunObservable
{
    //싱글턴 패턴을 위한 인스턴스 변수 선언
    public static GameMgr Inst = null;
    private PhotonView pv;

    JoyStickType m_JoyStickType = JoyStickType.Fixed;

    //-----------Fixed JoyStick 처리 부분
    public GameObject m_JoySBackObj = null;
    public Image m_JoyStickImg = null;
    float m_Radius = 0.0f;
    Vector3 m_OrignPos = Vector3.zero;
    Vector3 m_Axis = Vector3.zero;
    Vector3 m_JsCacVec = Vector3.zero;
    float m_JsCacDist = 0.0f;
    //-----------Fixed JoyStick 처리 부분

    [HideInInspector] public static Hero_Ctrl m_refHero = null;

    //-----------------피킹 이동 관련 변수
    Ray a_MousePos;
    RaycastHit hitInfo;
    private LayerMask m_layerMask = -1;
    //-----------------피킹 이동 관련 변수

    public GameObject m_CursorMark = null;
    Vector3 a_CacVLen = Vector3.zero;

    [Header("--- Shader ---")]
    public Shader g_AddTexShader = null;   //주인공 데미지 연출용(빨간색으로 변했다 돌아올 때)
    public Shader g_VertexLitShader = null; //몬스터 사망시 투명하게 사라지게하기 용

    [Header("--- DamageText ---")]
    //----------------- 머리위에 데미지 띄우기용 변수 선언
    public Transform m_Damage_Canvas = null;
    public GameObject m_DamagePrefab = null;
    RectTransform CanvasRect;
    Vector2 screenPos = Vector2.zero;
    Vector2 WdScPos = Vector2.zero;
    //-------------- DamageTxt 카메라 반대편에 있을 때 컬링하기 위한 변수들...
    GameObject[] m_DmgTxtList = null;
    Vector3 a_CacWdPos = Vector3.zero;
    Vector3 a_CacTgVec = Vector3.zero;
    //-------------- DamageTxt 카메라 반대편에 있을 때 컬링하기 위한 변수들...
    //----------------- 머리위에 데미지 띄우기용 변수 선언

    public Button m_Attack_Btn = null;
    public Button m_Skill_Btn = null;

    //-------------- 스킬 쿨 타임 적용
    private Text    m_Skill_Cool_Label = null;
    private Image   m_Skell_Cool_Mask = null;
    private Button  m_SkillUIBtn = null;
    [HideInInspector] public float m_Skill_Cooltime = 0.0f;
    float m_SkillCoolLen = 7.0f;
    //-------------- 스킬 쿨 타임 적용

    [Header("--- Button Handle ---")]
    public Button m_BackBtn;

    //---------------------------- UserInfo UI 관련 변수
    private bool m_UInfo_OnOff = false;
    [Header("--- UserInfo ---")]
    public Button m_UserInfo_Btn = null;
    public GameObject m_UserInfoPanel = null;
    public Text m_BestGPoint_Txt;
    public Text m_CurGPoint_Txt;
    public Text m_MonKill_Txt;
    public Text m_DiaCount_Txt;
    //---------------------------- UserInfo UI 관련 변수

    //----- 몬스터 스폰 관련 변수
    [Header("--- MonSpwan ---")]
    public GameObject[] m_SpwanPoint;
    public GameObject[] m_MonsterPrefab;
    string[] m_MonName = { "SKELETON_Root", "Alien_Root" };
    [HideInInspector] public int m_MonNum = 0;
    [HideInInspector] public int m_MonIndexNum = 0;
    int m_MonMaxNum = 5;
    [HideInInspector] public float m_MonSpwanTime = 0.0f;
    [HideInInspector] public bool m_MonSpwanOnOff = false;
    [HideInInspector] public float[] m_ReSpwanTime;
    int m_SetSpwanNum = 0;
    public GameObject m_MonsterGroup;
    //----- 몬스터 스폰 관련 변수

    //----- 채팅부분    
    public Text txtLogMsg;
    public InputField textChat;
    [HideInInspector] public bool bEnter = false;
    //-----

    void Awake()
    {
        //GameMgr 클래스를 인스턴스에 대입
        Inst = this;

        //PhotonView 컴포넌트 할당
        pv = GetComponent<PhotonView>();

        //주인공을 생성하는 함수 호출
        CreateHero();
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("게임 난이도 : " + ((int)GlobalValue.g_GameDifficulty + 1));

        PhotonNetwork.IsMessageQueueRunning = true;

        if (PhotonNetwork.IsMasterClient == true)
            m_MonSpwanTime = 3.0f;

        //로그 메시지에 출력할 문자열 생성
        string msg = "\n<color=#00ff00>[" + PhotonNetwork.LocalPlayer.NickName
                + "] Connected</color>";
        //RPC 함수 호출
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);

        m_layerMask = 1 << LayerMask.NameToLayer("MyTerrain");  //MyTerrain 만 피킹
        m_layerMask |= 1 << LayerMask.NameToLayer("MyUnit"); //Unit

        //-----------Fixed JoyStick 처리 부분
        if (m_JoySBackObj != null && m_JoyStickImg != null
            && m_JoySBackObj.activeSelf == true)
        {
            m_JoyStickType = JoyStickType.Fixed;

            Vector3[] v = new Vector3[4];
            m_JoySBackObj.GetComponent<RectTransform>().GetWorldCorners(v);
            //[0]:좌측하단 [1]:좌측상단 [2]:우측상단 [3]:우측하단
            //v[0] 촤측하단이 0, 0 좌표인 스크린 좌표(Screen.width, Screen.height)를 기준으로   
            m_Radius = v[2].y - v[0].y;
            m_Radius = m_Radius / 3.0f;

            m_OrignPos = m_JoyStickImg.transform.position;

            //스크립트로만 대기하고자 할 때
            EventTrigger trigger = m_JoySBackObj.GetComponent<EventTrigger>(); // Inspector에서 GameObject.Find("Button"); 에 꼭 AddComponent--> EventTrigger 가 되어 있어야 한다.
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.Drag;
            entry.callback.AddListener((data) => { 
                            OnDragJoyStick((PointerEventData)data); });
            trigger.triggers.Add(entry);

            entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.EndDrag;
            entry.callback.AddListener((data) => { 
                           OnEndDragJoyStick((PointerEventData)data); });
            trigger.triggers.Add(entry);
        }
        //-----------Fixed JoyStick 처리 부분

        //------ Skill Button 처리 코드
        m_Skill_Cooltime = 0.0f;

        if (m_Skill_Btn != null)
        {
            m_Skill_Btn.onClick.AddListener(() =>
            {
                if (m_refHero != null)
                    m_refHero.SkillOrder("RainArrow", ref m_SkillCoolLen, ref m_Skill_Cooltime);
            });

            m_Skill_Cool_Label = m_Skill_Btn.transform.GetComponentInChildren<Text>(true);
            m_Skell_Cool_Mask = m_Skill_Btn.transform.Find("SkillCoolMask").GetComponent<Image>();

            m_SkillUIBtn = m_Skill_Btn.GetComponent<Button>();
        }
        //------ Skill Button 처리 코드

        //------ Attack Button 처리 코드
        if (m_Attack_Btn != null)
            m_Attack_Btn.onClick.AddListener(() =>
            {
                if (m_refHero != null)
                    m_refHero.AttackOrder();
            });
        //------ Attack Button 처리 코드

        if (m_BackBtn != null)
            m_BackBtn.onClick.AddListener(OnClickBackBtn);

        //--------------- UserInfoPanel_OnOff
        if (m_UserInfo_Btn != null && m_UserInfoPanel != null)
        {
            m_UserInfo_Btn.onClick.AddListener(() => {

                m_UInfo_OnOff = !m_UInfo_OnOff;
                m_UserInfoPanel.SetActive(m_UInfo_OnOff);
            });
        }//if(m_UserInfo_Btn != null && m_UserInfoRoot != null)
        //--------------- UserInfoPanel_OnOff
        m_SetSpwanNum = SetMonSpwan(GlobalValue.g_GameDifficulty);

        for (int i = 0; i < m_SetSpwanNum; i++)
        {
            m_SpwanPoint[i].SetActive(true);
        }

        m_ReSpwanTime = new float[15];
    } //void Start()

    // Update is called once per frame
    void Update()
    {
        //-----몬스터 스폰
        if (PhotonNetwork.IsMasterClient == true)
        {
            if (0.0f < m_MonSpwanTime)
            {
                m_MonSpwanTime -= Time.deltaTime;
                if (m_MonSpwanTime < 0.0f)
                    m_MonSpwanOnOff = true;
            }

            if (m_MonNum < m_MonMaxNum && m_MonSpwanOnOff == true)
            {
                SpwanMonster();
            }
        }
        //-----몬스터 스폰

        //-----몬스터 리스폰
        for (int i = 0; i < m_SpwanPoint.Length; i++)
        {
            if (m_SpwanPoint[i].activeSelf == false)
                continue;

            if (0.0f < m_ReSpwanTime[i])
            {
                m_ReSpwanTime[i] -= Time.deltaTime;
                if (m_ReSpwanTime[i] < 0.0f)
                {
                    CreateMon(m_SpwanPoint[i].transform.position, i);
                }
            }
        }
        //-----몬스터 리스폰

        //-----채팅
        if (Input.GetKeyDown(KeyCode.Return)) //<---엔터치면 인풋 필드 활성활
        {
            bEnter = !bEnter;

            if (bEnter == true)
            {
                textChat.gameObject.SetActive(bEnter);
                textChat.ActivateInputField(); // 커서를 인풋필드로 이동시켜줌
            }
            else
            {
                textChat.gameObject.SetActive(bEnter);

                if (textChat.text != "")
                {
                    EnterChar();
                }
            }
        }//if(Input.GetKeyDown(KeyCode.Return))
        //-----

        Sill_Cooltime(ref m_Skill_Cooltime, ref m_Skill_Cool_Label,
                                            ref m_Skell_Cool_Mask, m_SkillCoolLen);

        //-----------------피킹 이동 부분 
        if (Input.GetMouseButtonDown(0))
        if (IsPointerOverUIObject() == false)
        {
            a_MousePos = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(a_MousePos, out hitInfo, Mathf.Infinity,
                                                                m_layerMask.value))
            {
                if (m_refHero != null)
                {

                        if (hitInfo.collider.gameObject.layer == LayerMask.NameToLayer("MyUnit")) //몬스터 피킹
                        {     //몬스터 픽킹일 때 
                            m_refHero.MousePicking(hitInfo.point, hitInfo.collider.gameObject);

                            if (m_CursorMark != null)
                                m_CursorMark.SetActive(false);
                        }
                        else  //지형 바닥 픽킹일 때 
                        {
                            m_refHero.MousePicking(hitInfo.point);

                            if (m_CursorMark != null)
                            {
                                m_CursorMark.transform.position =
                                    new Vector3(hitInfo.point.x, hitInfo.point.y + 0.01f, hitInfo.point.z);
                                //new Vector3(hitInfo.point.x, hitInfo.point.y + 0.2f, hitInfo.point.z);
                                m_CursorMark.SetActive(true);
                            }
                        }//else  //지형 바닥 픽킹일 때 



                    }//if (m_refHero != null)
            }
        }//if (Input.GetMouseButtonDown(0))
        //-----------------피킹 이동 부분

        //---클릭마크 끄기
        if (m_CursorMark != null && m_CursorMark.activeSelf == true)
        {
            if (m_refHero != null) //아직 죽지 않았을 때 
            {
                a_CacVLen = m_refHero.transform.position - 
                            m_CursorMark.transform.position;
                a_CacVLen.y = 0.0f;
                if (a_CacVLen.magnitude < 1.0f)
                    m_CursorMark.SetActive(false);
            }//if (m_TankComp != null) //아직 죽지 않았을 때 
        }//if (m_RTS_Arrow != null)
        //---클릭마크 끄기

    }//void Update()

    void LateUpdate()
    {
        m_DmgTxtList = GameObject.FindGameObjectsWithTag("DamageTxt");
        for (int i = 0; i < m_DmgTxtList.Length; ++i)
        {
            a_CacWdPos = m_DmgTxtList[i].GetComponent<DamageText>().m_BaseWdPos;
            a_CacTgVec = a_CacWdPos - Camera.main.transform.position;
            if (a_CacTgVec.magnitude <= 0.0f)
            {
                m_DmgTxtList[i].GetComponentInChildren<Text>().enabled = false;
            }
            else if (Vector3.Dot(Camera.main.transform.forward, a_CacTgVec.normalized) < 0.2f)  //80도 정도 밖같쪽이면 //-0.7f) //-45도를 넘는 범위에 있다는 뜻
            {  //80도 정도 밖같쪽이면
                m_DmgTxtList[i].GetComponentInChildren<Text>().enabled = false;
            }
            else
            {
                m_DmgTxtList[i].GetComponentInChildren<Text>().enabled = true;
            }

        }//for (int i = 0; i < m_DmgTxtList.Length; ++i)
    }


    //-----------Fixed JoyStick 처리 부분   using UnityEngine.EventSystems;
    void OnDragJoyStick(PointerEventData _data) //Delegate
    {
        if (m_JoyStickImg == null)
            return;

        m_JsCacVec = Input.mousePosition - m_OrignPos;
        m_JsCacVec.z = 0.0f;
        m_JsCacDist = m_JsCacVec.magnitude;
        m_Axis = m_JsCacVec.normalized;

        //조이스틱 백그라운드를 벗어나지 못하게 막는 부분
        if (m_Radius < m_JsCacDist)
        {
            m_JoyStickImg.transform.position =
                                    m_OrignPos + m_Axis * m_Radius;
        }
        else
        {
            m_JoyStickImg.transform.position =
                                    m_OrignPos + m_Axis * m_JsCacDist;
        }

        if (1.0f < m_JsCacDist)
            m_JsCacDist = 1.0f;

        //캐릭터 이동 처리
        if (m_refHero != null)
            m_refHero.SetJoyStickMv(m_JsCacDist, m_Axis);
    }

    void OnEndDragJoyStick(PointerEventData _data) //Delegate
    {
        if (m_JoyStickImg == null)
            return;

        m_Axis = Vector3.zero;
        m_JoyStickImg.transform.position = m_OrignPos;

        m_JsCacDist = 0.0f;

        //캐릭터 정지 처리
        if (m_refHero != null)
            m_refHero.SetJoyStickMv(0.0f, m_Axis);
    }
    //-----------Fixed JoyStick 처리 부분

    PointerEventData a_EDCurPos; // using UnityEngine.EventSystems;
    public bool IsPointerOverUIObject() //UGUI의 UI들이 먼저 피킹되는지 확인하는 함수
    {
        a_EDCurPos = new PointerEventData(EventSystem.current);
#if !UNITY_EDITOR && (UNITY_IPHONE || UNITY_ANDROID)
            //using System.Collections.Generic;
            List<RaycastResult> results = new List<RaycastResult>();
            for (int i = 0; i < Input.touchCount; ++i)
            {
                a_EDCurPos.position = Input.GetTouch(i).position; 
                results.Clear();

                EventSystem.current.RaycastAll(a_EDCurPos, results);
                if (0 < results.Count)
                    return true;
            }
            return false;
#else
        a_EDCurPos.position = Input.mousePosition;
        //using System.Collections.Generic;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(a_EDCurPos, results);
        return results.Count > 0;
#endif
    }

    Vector3 a_StCacPos = Vector3.zero;
    public void SpawnDamageTxt(int dmg, Transform txtTr, int a_ColorIdx = 0)
    {
        if (m_DamagePrefab != null && m_Damage_Canvas != null)
        {
            GameObject m_DamageObj = (GameObject)Instantiate(m_DamagePrefab);
            a_StCacPos = new Vector3(txtTr.position.x, 
                                     txtTr.position.y + 2.65f, txtTr.position.z);

            m_DamageObj.transform.SetParent(m_Damage_Canvas, false);
            DamageText a_DamageTx = m_DamageObj.GetComponent<DamageText>();
            a_DamageTx.m_BaseWdPos = a_StCacPos;
            a_DamageTx.m_DamageVal = (int)dmg;

            //초기 위치 잡아 주기 //--World 좌표를 UGUI 좌표로 환산해 주는 코드
            CanvasRect = m_Damage_Canvas.GetComponent<RectTransform>();
            screenPos = Camera.main.WorldToViewportPoint(a_StCacPos);
            WdScPos.x = ((screenPos.x * CanvasRect.sizeDelta.x) - 
                                        (CanvasRect.sizeDelta.x * 0.5f));
            WdScPos.y = ((screenPos.y * CanvasRect.sizeDelta.y) - 
                                        (CanvasRect.sizeDelta.y * 0.5f));
            m_DamageObj.GetComponent<RectTransform>().anchoredPosition = WdScPos;
            //--World 좌표를 UGUI 좌표로 환산해 주는 코드

            if (a_ColorIdx == 1) //주인공 일때 데미지 택스트 색 바꾸기...
            {
                Outline a_Outline = m_DamageObj.GetComponentInChildren<Outline>();
                a_Outline.effectColor = new Color32(255, 255, 255, 0);   // new Color32(255, 255, 255, 120);  //new Color32(0, 100, 255, 255);
                a_Outline.enabled = false;

                Text a_RefText = m_DamageObj.GetComponentInChildren<Text>();
                a_RefText.color = new Color32(255, 255, 230, 255);
            }
        }//if (m_DamagePrefab != null && m_Damage_Canvas != null)
    }// public void DamageTxt(

    void Sill_Cooltime(ref float Cool_float, ref Text Cool_Label,
                                     ref Image Cool_Sprite, float Max_Cool)
    {
        if (0.0f < Cool_float)
        {
            Cool_float -= Time.deltaTime;
            Cool_Label.text = ((int)Cool_float).ToString();
            Cool_Sprite.fillAmount = Cool_float / Max_Cool;

            if (m_SkillUIBtn != null)
                m_SkillUIBtn.enabled = false;
        }
        else
        {
            Cool_float = 0.0f;
            Cool_Sprite.fillAmount = 0.0f;
            Cool_Label.text = "";

            if (m_SkillUIBtn != null)
                m_SkillUIBtn.enabled = true;
        }
    }//void Sill_Cooltime(ref float Cool_float, ref Text Cool_Label, ref Image Cool_Sprite, float Max_Cool)

    //룸 나가기 버튼 클릭 이벤트에 연결될 함수
    void OnClickBackBtn()
    {
        //로그 메시지에 출력할 문자열 생성
        string msg = "\n<color=#ff0000>[" + PhotonNetwork.LocalPlayer.NickName
                + "] Disconnected</color>";
        //RPC 함수 호출
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);

        //마지막 사람이 방을 떠날 때 룸의 CustomProperties를 초기화 해 주어야 한다.
        if (PhotonNetwork.PlayerList != null && PhotonNetwork.PlayerList.Length <= 1)
        {
            if (PhotonNetwork.CurrentRoom != null)
                PhotonNetwork.CurrentRoom.CustomProperties.Clear();
        }

        //지금 나가려는 탱크를 찾아서 그 탱크의  
        //모든 CustomProperties를 초기화 해 주고 나가는 것이 좋다. 
        //(그렇지 않으며 나갔다 즉시 방 입장시 오류 발생한다.)
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.CustomProperties.Clear();
        //그래야 중개되던 것이 모두 초기화 될 것이다.

        //현재 룸을 빠져나가며 생성한 모든 네트워크 객체를 삭제
        PhotonNetwork.LeaveRoom();
    }

    //룸에서 접속 종료됐을 때 호출되는 콜백 함수
    public override void OnLeftRoom()  //PhotonNetwork.LeaveRoom(); 성공했을 때 
    {
        Time.timeScale = 1.0f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("scLobby"); //로비 씬을 호출
    }

    //주인공을 생성하는 함수 
    void CreateHero()
    {
        Vector3 a_HPos = Vector3.zero;
        Vector3 a_AddPos = Vector3.zero;
        GameObject a_HPosObj = GameObject.Find("HeroSpawnPos");
        if (a_HPosObj != null)
        {
            a_AddPos.x = Random.Range(-5.0f, 5.0f);
            a_AddPos.z = Random.Range(-5.0f, 5.0f);
            a_HPos = a_HPosObj.transform.position + a_AddPos;
        }

        PhotonNetwork.Instantiate(GlobalValue.g_UserType.ToString(), a_HPos, Quaternion.identity, 0);;
    }

    void SpwanMonster()
    {
        for(int i = 0; i < m_SpwanPoint.Length; i++)
        {
            if (m_SpwanPoint[i].activeSelf == false)
                continue;

            CreateMon(m_SpwanPoint[i].transform.position, i);
        }
    }

    public void CreateMon(Vector3 _MPos, int _Num)
    {
        int a_RanMon = Random.Range(0,2);

        m_MonIndexNum = _Num;

        if (m_SpwanPoint[_Num] != null)
            m_SpwanPoint[_Num].SetActive(false);

        Quaternion m_Rot = Quaternion.identity;
        m_Rot.y = 180.0f;
        MonsterCtrl a_MonCtrl = null;
        GameObject a_Mon = null;

        a_Mon = PhotonNetwork.InstantiateRoomObject(m_MonName[a_RanMon], _MPos, m_Rot, 0);

        a_Mon.transform.parent = m_MonsterGroup.transform;

        if (a_Mon != null)
        {
            a_MonCtrl = a_Mon.GetComponent<MonsterCtrl>();
            a_MonCtrl.m_MonIndexNum = this.m_MonIndexNum;
        }
        m_MonNum++;

        if (m_MonMaxNum <= m_MonNum)
            m_MonSpwanOnOff = false;
    }

    void EnterChar()
    {
        string msg = "\n<color=#ffffff>[" + PhotonNetwork.LocalPlayer.NickName + "]"
                    + textChat.text + "</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);

        textChat.text = "";
    }
    //룸 접속자 정보를 조회하는 함수
    void GetConnectPlayerCount()
    {
        //현재 입장한 룸 정보를 받아옴
        Room currRoom = PhotonNetwork.CurrentRoom; //using Photon.Realtime;
        m_SetSpwanNum = SetMonSpwan(GlobalValue.g_GameDifficulty);
    }

    //네트워크 플레이어가 접속했을 대 호출되는 함수
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        GetConnectPlayerCount();
    }

    //네트워크 플레이어가 룸을 나가거나 접속이 끊어졌을 때 호출되는 함수
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        GetConnectPlayerCount();
    }

    private void OnApplicationFocus(bool focus) //윈도우 창 활성화 비활성화 일때
    {
        PhotonInit.isFocus = focus;
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            for(int i = 0; i < m_SpwanPoint.Length; i++)
            {
                stream.SendNext(m_SpwanPoint[i].activeSelf);
                stream.SendNext(m_ReSpwanTime[i]);
            }
            stream.SendNext((int)GlobalValue.g_GameDifficulty);
        }
        else //원격 플레이어의 위치 정보 수신
        {
            for (int i = 0; i < m_SpwanPoint.Length; i++)
            {
                m_SpwanPoint[i].SetActive((bool)stream.ReceiveNext());
                m_ReSpwanTime[i] = (float)stream.ReceiveNext();
            }
            GlobalValue.g_GameDifficulty = (GameDifficulty)stream.ReceiveNext();
        }
    }

    int SetMonSpwan(GameDifficulty _Stage)
    {
        int a_MonNum = 0;
        if(_Stage == GameDifficulty.Stage_1)
        {
            a_MonNum = 5;
        }
        else if(_Stage == GameDifficulty.Stage_2)
        {
            a_MonNum = 9;
        }
        else if(_Stage == GameDifficulty.Stage_3)
        {
            a_MonNum = 11;
        }
        else if(_Stage == GameDifficulty.Stage_4)
        {
            a_MonNum = 13;
        }
        else if(_Stage == GameDifficulty.Stage_5)
        {
            a_MonNum = 15;
        }

        return a_MonNum;
    }

    [PunRPC]
    void LogMsg(string msg)
    {
        //로그 메시지 Text UI에 텍스트를 누적시켜 표시
        txtLogMsg.text = txtLogMsg.text + msg;
    }
}
