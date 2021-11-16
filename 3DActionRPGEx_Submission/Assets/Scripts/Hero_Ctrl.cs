using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Photon.Pun;

//if(pv.IsMine == true) 일 때 PC일 때 
//PhotonNetwork.Instantiate() -> void Awake() -> void Start()

//if(pv.IsMine == false) 일 때 Other Client일 때 
//PhotonNetwork.Instantiate() -> void Awake() -> public void OnPhotonSerializeView() 
//-> void Start()

public enum CtrlMode
{
    Immediate,
    S_Lock,     //스킬 예약
    AS_Lock     //공격, 스킬 예약
}

public class Hero_Ctrl : MonoBehaviourPunCallbacks, IPunObservable
{
    //------------------------ HP바 표시
    [HideInInspector] public float Curhp = 100;
    [HideInInspector] public float Maxhp = 100;
    float NetHp = 100;  //CurHP 중계용(Update()함수에서 죽는 연출을 한번만 실행 시켜주기 위해서...)
    public Image imgHpbar;      //using UnityEngine.UI;
    //------------------------ HP바 표시

    float m_MoveVelocity = 5.0f;     //평면 초당 이동 속도...

    //---------- 키보드 입력값 변수 선언
    float h = 0, v = 0;
    Vector3 MoveNextStep;            //보폭을 계산해 주기 위한 변수
    Vector3 MoveHStep;
    Vector3 MoveVStep;

    float a_CalcRotY = 0.0f;
    float rotSpeed = 150.0f; //초당 150도 회전하라는 속도
    //---------- 키보드 입력값 변수 선언  

    //------ JoyStick 이동 처리 변수
    private float m_JoyMvLen = 0.0f;
    private Vector3 m_JoyMvDir = Vector3.zero;
    //------ JoyStick 이동 처리 변수

    //------ Picking 관련 변수 
    private Vector3 m_MoveDir = Vector3.zero;   //평면 진행 방향
    protected float m_RotSpeed = 7.0f;          //초당 회전 속도

    private bool m_isPickMvOnOff = false;       //피킹 이동 OnOff
    private Vector3 m_TargetPos = Vector3.zero; //최종 목표 위치
    private double m_MoveDurTime = 0.0;         //목표점까지 도착하는데 걸리는 시간
    private double m_AddTimeCount = 0.0;        //누적시간 카운트 
    Vector3 a_StartPos = Vector3.zero;
    Vector3 a_CacLenVec = Vector3.zero;
    Quaternion a_TargetRot;
    //------ Picking 관련 변수 

    public Anim anim;  //AnimSupporter.cs 쪽에 정의되어 있음
    Animator m_RefAnimator = null;
    AnimatorStateInfo animaterStateInfo;
    string m_prevState = "";

    //------ 공격시 방향전환용 변수들
    GameObject[] m_EnemyList = null;

    float   m_AttackDist = 1.9f;
    private GameObject m_TargetUnit = null;

    Vector3 a_CacTgVec = Vector3.zero;
    Vector3 a_CacAtDir = Vector3.zero;   //공격시 방향전환용 변수
    //------ 공격시 방향전환용 변수들

    //------ 데미지 계산용 변수들...
    float a_fCacLen = 0.0f;
    int iCount = 0;
    GameObject a_EffObj = null;
    Vector3 a_EffPos = Vector3.zero;
    //------ 데미지 계산용 변수들...

    //-------------데미지 칼라 관련 변수
    private Shader g_DefTexShader = null;
    private Shader g_WeaponTexShader = null;

    private bool AttachColorChange = false;
    private SkinnedMeshRenderer m_SMR = null;
    private SkinnedMeshRenderer[] m_SMRList = null;
    private MeshRenderer[] m_MeshList = null;          //장착 무기
    private float AttachColorStartTime = 0f;
    private float AttachColorTime = 0.2f;
    private float a_Ratio = 0.0f;
    private float a_fCol = 0.0f;
    private float a_DamageColor = 0.73f;
    private Color a_CalcColor;
    //-------------데미지 칼라 관련 변수

    //---------------------- 마우스 피킹 이동 예약
    public CtrlMode m_CtrlMode = CtrlMode.S_Lock;
    private float m_RsvPicking = 0.0f;             //reservation 예약 //피킹 이동 예약
    private Vector3 m_RsvTargetPos = Vector3.zero; //최종 목표 위치
    private double m_RsvMvDurTime = 0.0;           //목표점까지 도착하는데 걸리는 시간
    private GameObject m_RsvTgUnit = null;         //타겟 유닛도 예약해 둔다.
    //---------------------- 마우스 피킹 이동 예약

    bool m_AttRotPermit = false;

    //---- Navigation
    protected NavMeshAgent nvAgent;    //using UnityEngine.AI;
    protected NavMeshPath movePath;

    protected Vector3 m_PathEndPos = Vector3.zero;
    [HideInInspector] public int m_CurPathIndex = 1;
    //---- Navigation

    //PhotonView 컴포넌트를 이용한 이동 동기화용 변수
    private Transform tr;
    [HideInInspector] public PhotonView pv = null;

    //위치 정보를 송수신할 때 사용할 변수 선언 및 초깃값 설정
    private Vector3 currPos = Vector3.zero;
    private Quaternion currRot = Quaternion.identity;
    AnimState m_CurState = AnimState.idle; //IsMine == true 일때
    //PhotonView 컴포넌트를 이용한 이동 동기화용 변수

    float m_IsOnece = 0.02f;
    int   m_DiaCount = 0;  //Dia == Diamond

    public Text m_NickName = null;

    void Awake()
    {
        tr = GetComponent<Transform>();

        //PhotonView 컴포넌트 할당
        pv = GetComponent<PhotonView>();
        pv.ObservedComponents[0] = this;
        // pv.ObservedComponents[1] = GetComponent<PhotonAnimatorView>();

        if (pv.IsMine)
            Camera.main.GetComponent<CameraCtrl>().InitCamera(this.gameObject);

        //원격 캐릭터의 위치 및 회전 값을 처리할 변수의 초깃값 설정
        currPos = tr.position;
        currRot = tr.rotation;
    }

    // Start is called before the first frame update.
    void Start()
    {
        if (m_NickName != null)
            m_NickName.text = pv.Owner.NickName;

        //주인공 고유 변수 초기화
        Maxhp = GlobalValue.g_UserHP;
        Curhp = Maxhp;
        NetHp = Maxhp; //CurHP 중계용(Update()함수에서 죽는 연출을 한번만 실행 시켜주기 위해서...)
        //주인공 고유 변수 초기화

        m_RefAnimator = this.gameObject.GetComponent<Animator>();

        movePath = new NavMeshPath();
        nvAgent = this.gameObject.GetComponent<NavMeshAgent>();
        nvAgent.updateRotation = false;

        if (pv.IsMine)
        {
            GameMgr.m_refHero = this;
        }

        FindDefShader();
        AttachColorTime = 0.1f;  //피격을 짧게 준다.
    }

    // Update is called once per frame
    void Update()
    {
        if (0.0f < m_RsvPicking)
            m_RsvPicking -= Time.deltaTime;

        AttackRotUpdate(); //공격애니메이션 중일 때 타겟을 향해 회전하게 하는 함수

        if (pv.IsMine)
        {
            KeyBDMove();
            JoyStickMvUpdate();
            MousePickUpdate();
        }//if (pv.IsMine)
        else //원격 플레이어일 때 수행
        {
            if (0.0f < m_IsOnece)
            {
                m_IsOnece -= Time.deltaTime;
                if (m_IsOnece <= 0.0f)
                {
                    if (nvAgent != null && nvAgent.enabled == true)
                    {
                        nvAgent.Warp(currPos); //워프 네비메쉬 Spawn 함수
                        tr.rotation = currRot;
                    }
                }
                else
                    return;
            }// if (0.0f < m_IsOnece)

            if (10.0f < (tr.position - currPos).magnitude)
            {
                tr.position = currPos;
            }
            else
            {
                //원격 플레이어의 플레이어를 수신받은 위치까지 부드럽게 이동시킴
                tr.position = Vector3.Lerp(tr.position, currPos, Time.deltaTime * 10.0f);
            }
            //원격 플레이어의 플레이어를 수신받은 각도만큼 부트럽게 회전시킴
            tr.rotation = Quaternion.Slerp(tr.rotation, currRot, Time.deltaTime * 10.0f);

            Remote_Animation();

            Remote_TakeDamage(); //Other Client Computer에서는 여기에서 Hp 동기화를 따라간다.

        } //if (pv.IsMine == false)

        DamageColorUpdate();
    }

    void KeyBDMove()      //키보드 이동
    {
        if (GameMgr.Inst.bEnter == true)
            return;

        //-------------- 가감속 없이 이동 처리 하는 방법
        h = Input.GetAxisRaw("Horizontal"); //화살표키 좌우키를 눌러주면 -1.0f, 0.0f, 1.0f 사이값을 리턴해 준다.
        v = Input.GetAxisRaw("Vertical");   //화살표키 위아래키를 눌러주면 -1.0f, 0.0f, 1.0f 사이값을 리턴해 준다.
        //-------------- 가감속 없이 이동 처리 하는 방법
        if (v < 0.0f)
            v = 0.0f;

        if (0.0f != h || 0.0f != v) //키보드 이동처리
        {
            if (m_CtrlMode == CtrlMode.AS_Lock)
            {
                if (ISAttack() == true)
                    return;
            }
            else if (m_CtrlMode == CtrlMode.S_Lock)
            {
                if (ISSkill() == true)
                    return;
            }

            //-------- 일반적인 이동 계산법
            a_CalcRotY = transform.eulerAngles.y;
            a_CalcRotY = a_CalcRotY + (h * rotSpeed * Time.deltaTime);
            transform.eulerAngles = new Vector3(0.0f, a_CalcRotY, 0.0f);

            MoveVStep = transform.forward * v;
            //MoveNextStep = MoveVStep.normalized * m_MoveVelocity * Time.deltaTime;
            //transform.position = transform.position + MoveNextStep;
            //-------- 일반적인 이동 계산법

            //---------------- 네비게이션 메시를 이용한 이동 방법
            MoveNextStep = MoveVStep;
            MoveNextStep = MoveNextStep.normalized * m_MoveVelocity;
            MoveNextStep.y = 0.0f;
            nvAgent.velocity = MoveNextStep;
            //---------------- 네비게이션 메시를 이용한 이동 방법

            MySetAnim(AnimState.move);

            ClearMsPickPath();
        }
        else
        {
            //키보드 이동중이 아닐 때만 아이들 동작으로 돌아가게 한다.
            if (m_isPickMvOnOff == false && m_JoyMvLen <= 0.0f && ISAttack() == false)
                MySetAnim(AnimState.idle);
        }
    }

    public void MySetAnim(AnimState newAnim, float CrossTime = 1.0f)
    {
        if (m_RefAnimator == null)
            return;

        if (m_prevState != null && !string.IsNullOrEmpty(m_prevState))
        {
            if (m_prevState.ToString() == newAnim.ToString())
                return;
        }

        if (!string.IsNullOrEmpty(m_prevState))
        {
            m_RefAnimator.ResetTrigger(m_prevState.ToString());
            m_prevState = null;
        }

        m_AttRotPermit = false;// 모든 애니메이션이 시작할 때 우선 꺼주고

        if (newAnim == AnimState.reAttack)
            newAnim = AnimState.attack;

        if (0.0f < CrossTime)
            m_RefAnimator.SetTrigger(newAnim.ToString());
        else
            m_RefAnimator.Play(newAnim.ToString(), -1, 0f); 
        //가운데는 Layer Index, 뒤에 0f는 처음부터 다시시작

        m_prevState = newAnim.ToString(); //이전스테이트에 현재스테이트 저장
        m_CurState  = newAnim;
    }

    Vector3 a_CacCamVec = Vector3.zero;
    Vector3 a_RightDir = Vector3.zero;

    public void SetJoyStickMv(float a_JoyMvLen, Vector3 a_JoyMvDir)
    {
        if (pv.IsMine == false)
            return;

        m_JoyMvLen = a_JoyMvLen;
        if (0.0f < a_JoyMvLen)
        {
            //--------카메라가 바라보고 있는 전면을 기준으로 회전 시켜줘야 한다. 
            a_CacCamVec = Camera.main.transform.forward;
            a_CacCamVec.y = 0.0f;
            a_CacCamVec.Normalize();
            m_JoyMvDir = a_CacCamVec * a_JoyMvDir.y; //위 아래 조작(카메라가 바라보고 있는 기준으로 위, 아래로 얼만큼 이동시킬 것인지?)
            a_RightDir = Camera.main.transform.right; //Vector3.Cross(Vector3.up, a_CacCamVec).normalized;
            m_JoyMvDir = m_JoyMvDir + (a_RightDir * a_JoyMvDir.x); //좌우 조작(카메라가 바라보고 있는 기준으로 좌, 우로 얼만큼 이동시킬 것인지?)
            m_JoyMvDir.y = 0.0f;
            m_JoyMvDir.Normalize();
            //--------카메라가 바라보고 있는 전면을 기준으로 회전 시켜줘야 한다. 

            //마우스 피킹 이동 취소
            ClearMsPickPath();
        }

        if (a_JoyMvLen == 0.0f)
        {
            //공격 애니메이션 중이면 공격 애니메이션이 끝나고 숨쉬기 애니로 돌아가게 한다.
            if (m_isPickMvOnOff == false && ISAttack() == false)
                MySetAnim(AnimState.idle);
        }
    }

    void JoyStickMvUpdate()
    {
        if (0.0f != h || 0.0f != v)
            return;

        ////--- 조이스틱 코드
        if (0.0f < m_JoyMvLen)  //조이스틱으로 움직일 때 
        {
            if (m_CtrlMode == CtrlMode.AS_Lock)
            {
                if (ISAttack() == true)
                    return;
            }
            else if (m_CtrlMode == CtrlMode.S_Lock)
            {
                if (ISSkill() == true)
                    return;
            }

            m_MoveDir = m_JoyMvDir;

            float amtToMove = m_MoveVelocity * Time.deltaTime;
            //캐릭터 스프링 회전 
            if (0.0001f < m_JoyMvDir.magnitude)
            {
                Quaternion a_TargetRot = Quaternion.LookRotation(m_JoyMvDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, a_TargetRot,
                                     Time.deltaTime * m_RotSpeed);
            }
            //캐릭터 스프링 회전   

            ////---------------- 일반적인 이동 계산법
            //MoveNextStep = m_JoyMvDir * (m_MoveVelocity * Time.deltaTime);
            //MoveNextStep.y = 0.0f;
            //transform.position = transform.position + MoveNextStep;
            ////---------------- 일반적인 이동 계산법

            //---------------- 네비게이션 메시를 이용한 이동 방법
            MoveNextStep = m_JoyMvDir * m_MoveVelocity;
            MoveNextStep.y = 0.0f;
            nvAgent.velocity = MoveNextStep;
            //---------------- 네비게이션 메시를 이용한 이동 방법

            MySetAnim(AnimState.move);
        }
        ////--- 조이스틱 코드
    }//void JoyStickMvUpdate()


    //마우스 좌측버튼 클릭시 호출될 함수
    public void MousePicking(Vector3 a_SetPickVec, GameObject a_PickMon = null)
    {
        if (pv.IsMine == false)
            return;

        //캐릭터들의 Hp바와 닉네임바 RaycastTarget을 모두 꺼주어야 피킹이 정상적으로 작동된다. 
        //그렇지 않으면 if (IsPointerOverUIObject() == false) 로 자꾸 막히게 된다.

        a_StartPos = this.transform.position; //출발 위치    

        a_SetPickVec.y = this.transform.position.y; // 최종 목표 위치

        a_CacLenVec = a_SetPickVec - a_StartPos;
        a_CacLenVec.y = 0.0f;

        //------- Picking Enemy 공격 처리 부분
        if (a_PickMon != null)
        {
            a_CacTgVec = a_PickMon.transform.position - transform.position;

            //공격가시거리... 타겟이 있고  +1.0f면 어차피 몬스터도 다가올꺼고 
            //좀 일찍 공격애니에 들어가야 잠시라도 move 애니가 끼어 들지 못한다.
            float a_AttDist = m_AttackDist; 
            //지금 공격하려고 하는 몬스터의 어그로 타겟이 내가 아니면...
            if (a_PickMon.GetComponent<MonsterCtrl>().m_AggroTarget 
                                                    == this.gameObject)
            {
                a_AttDist = m_AttackDist + 1.0f; 
                //지금 공격하려고 하는 몬스터의 어그로 타겟이 나면....
            }

            if (a_CacTgVec.magnitude <= a_AttDist)   
            {
                m_TargetUnit = a_PickMon;
                AttackOrder();  //즉시 공격

                return;

            }//즉시 공격 하라
        }
        //------- Picking Enemy 공격 처리 부분

        if (a_CacLenVec.magnitude < 0.5f)  //너무 근거리 피킹은 스킵해 준다.
            return;

        //---네비게이션 메쉬 길찾기를 이용할 때 코드
        float a_PathLen = 0.0f;
        if (MyNavCalcPath(a_StartPos, a_SetPickVec, ref a_PathLen) == false)
            return;
        //---네비게이션 메쉬 길찾기를 이용할 때 코드

        m_TargetPos = a_SetPickVec;   // 최종 목표 위치
        m_isPickMvOnOff = true;       //피킹 이동 OnOff

        m_MoveDir = a_CacLenVec.normalized;
        //---네비게이션 메쉬 길찾기를 이용했을 때 거리 계산법
        m_MoveDurTime = a_PathLen / m_MoveVelocity; //도착하는데 걸리는 시간
        //---네비게이션 메쉬 길찾기를 이용했을 때 거리 계산법

        ////---일반적으로 이용했을 때 거리 계산법
        //m_MoveDurTime = a_CacLenVec.magnitude / m_MoveVelocity;
        ////도착하는데 걸리는 시간
        ////---일반적으로 이용했을 때 거리 계산법
        m_AddTimeCount = 0.0;

        m_TargetUnit = a_PickMon; //타겟 초기화 또는 무효화 

        //공격 중일 때 몬스터를 계속 클릭하면 다음 공격이 예약 되도록...
        //근처에 공격할 몬스터가 있다면....
        if (m_CtrlMode == CtrlMode.AS_Lock)
        {
            if (ISAttack() == true)
            {
                m_RsvPicking = 3.5f;  //지금 이 예약의 유효시간 3.5초 뒤에 무효화 됨
                m_RsvTargetPos = m_TargetPos;
                m_RsvMvDurTime = m_MoveDurTime;
                m_RsvTgUnit = a_PickMon;
                ClearMsPickPath();    //m_isPickMvOnOff = false
                m_MoveDurTime = 0.0f;
            }
        }
        else if (m_CtrlMode == CtrlMode.S_Lock)
        {
            if (ISSkill() == true)
            {
                m_RsvPicking = 3.5f;  //지금 이 예약의 유효시간 3.5초 뒤에 무효화 됨
                m_RsvTargetPos = m_TargetPos;
                m_RsvMvDurTime = m_MoveDurTime;
                m_RsvTgUnit = a_PickMon;
                ClearMsPickPath();    //m_isPickMvOnOff = false
                m_MoveDurTime = 0.0f;
            }
        }

    }//public void MousePicking()

    void MousePickUpdate()  //<--- MousePickMove()
    {
        ////-------------- 마우스 피킹 이동
        if (m_isPickMvOnOff == true)
        {
            //---네비게이션 메쉬 길찾기를 이용할 때 코드
            m_isPickMvOnOff = MoveToPath(); //도착한 경우 false 리턴함
            //---네비게이션 메쉬 길찾기를 이용할 때 코드

            ////---길찾기를 안하고 이동할 때 코드
            //a_CacLenVec = m_TargetPos - this.transform.position;
            //a_CacLenVec.y = 0.0f;

            ////캐릭터 스프링 회전 
            //if (0.1f < a_CacLenVec.magnitude)  //로테이션에서는 모두 들어가야 한다.
            //{
            //    m_MoveDir = a_CacLenVec.normalized;

            //    a_TargetRot = Quaternion.LookRotation(m_MoveDir);
            //    transform.rotation = Quaternion.Slerp(transform.rotation, a_TargetRot,
            //                                          Time.deltaTime * m_RotSpeed);
            //}
            ////캐릭터 스프링 회전   

            //m_AddTimeCount = m_AddTimeCount + Time.deltaTime;
            //if (m_MoveDurTime <= m_AddTimeCount) //목표점에 도착한 것으로 판정한다.
            //{
            //    //마우스 피킹 이동 취소
            //    ClearMsPickPath();  //m_isPickMvOnOff = false;

            //    if (ISAttack() == false)         //공격이 끝났을 때만 Idle로 넘어가도록...
            //        MySetAnim(AnimState.idle);
            //}
            //else
            //{
            //    ////---------------- 일반적인 이동 계산법
            //    this.transform.position = this.transform.position +
            //                             (m_MoveDir * Time.deltaTime * m_MoveVelocity);
            //    ////---------------- 일반적인 이동 계산법

            //    MySetAnim(AnimState.move); //"run" 애니메이션 적용

            //}
            ////---길찾기를 안하고 이동할 때 코드

            //------ 타겟을 향해 피킹 이동 공격
            if (m_TargetUnit != null)
            { //<-- 공격 애니매이션 중이면 가장 가까운 타겟을 자동으로 잡게된다.
                a_CacTgVec = m_TargetUnit.transform.position - this.transform.position;
                if (a_CacTgVec.magnitude <= m_AttackDist) //공격거리
                    AttackOrder();
            }
            //------ 타겟을 향해 피킹 이동 공격

        }//if (m_isPickMvOnOff == true)
    }//void MousePickUpdate() 


    void ClearMsPickPath() //마우스 픽킹이동 취소 함수
    {
        m_isPickMvOnOff = false;

        //----피킹을 위한 동기화 부분
        m_PathEndPos = transform.position;
        if (0 < movePath.corners.Length)
        {
            movePath.ClearCorners();  //경로 모두 제거 
        }
        m_CurPathIndex = 1;       //진행 인덱스 초기화
        //----피킹을 위한 동기화 부분

        if (GameMgr.Inst.m_CursorMark != null)
            GameMgr.Inst.m_CursorMark.SetActive(false);
    }

    public void AttackOrder()
    {
        if (pv.IsMine == false)
            return;

        if (m_prevState == AnimState.idle.ToString()
            || m_prevState == AnimState.move.ToString())
        {
            //Immediate 모드이고 키보드 컨트롤이나 조이스틱 컨트롤로 이동 중이고
            //공격키를 연타해서 누르면 달리는 애니메이션에 잠깐동안 
            //공격 애니가 끼어드는 문제가발생한다. <-- 이런 현상의 예외처리
            if ((0.0f != h || 0.0f != v) || 0.0f < m_JoyMvLen)
            {
                return;
            }

            MySetAnim(AnimState.attack);

            ClearMsPickPath();
        }
    }

    public bool ISAttack()
    {
        if (m_prevState != null && !string.IsNullOrEmpty(m_prevState))
        {
            if (m_prevState.ToString() == AnimState.attack.ToString() ||
                m_prevState.ToString() == AnimState.skill.ToString())
            {
                return true;
            }
        }

        return false;
    }

    public bool ISSkill()
    {
        if (m_prevState != null && !string.IsNullOrEmpty(m_prevState))
        {
            if (m_prevState.ToString() == AnimState.skill.ToString())
            {
                return true;
            }
        }

        return false;
    }

    //---> 이벤트 함수로 추가 해 주어야 한다.
    public void IsAttackFinish(string Type) //공격애니메이션 끝났는지? 판단하는 함수
    {
        if (pv.IsMine == false)
            return;

        if ((0.0f != h || 0.0f != v) || 0.0f < m_JoyMvLen
                || m_isPickMvOnOff == true)//키보드 이동조작이 있거나, 조이스틱 조작이 있는 경우
        { //스킬 사용 중에 이동을 누르고 있다가 이쪽으로 들어오면 예약 즉시 취소를 위한 코드
            MySetAnim(AnimState.move);
            m_RsvPicking = 0.0f;
            return;
        }
        else if (0.0f < m_RsvPicking) //마우스 피킹 예약이 있었다면...
        {
            //--- 네비게이션 메쉬 길찾기를 이용할 때 코드
            m_TargetUnit = m_RsvTgUnit;  //타겟도 바꾸거나 무효화 시켜 버린다.
            a_StartPos = this.transform.position; //출발 위치     
            m_TargetPos = m_RsvTargetPos;
            m_TargetPos.y = this.transform.position.y; // 최종 목표 위치

            float a_PathLen = 0.0f;
            if (MyNavCalcPath(a_StartPos, m_TargetPos, ref a_PathLen) == true)
            {
                m_isPickMvOnOff = true;                 //피킹 이동 OnOff
                a_CacLenVec = m_TargetPos - a_StartPos;
                a_CacLenVec.y = 0.0f;
                m_MoveDir = a_CacLenVec.normalized;
                m_MoveDurTime = a_PathLen / m_MoveVelocity; //도착하는데 걸리는 시간
                m_AddTimeCount = 0.0;
            }
            //--- 네비게이션 메쉬 길찾기를 이용할 때 코드

            ////---길찾기를 이용하지 않고 예약 이동 처리를 할 때 코드
            //m_TargetUnit = m_RsvTgUnit;  //타겟도 바꾸거나 무효화 시켜 버린다.
            //m_RsvPicking = 0.0f;         
            ////스킬 끝나는 시점에 한번만 적용되니까 
            ////스킬 끝나는 시점에 키보드 마우스 이동이 있으면 다음 Update에서 취소될 것이다.
            //m_TargetPos  = m_RsvTargetPos;
            //m_MoveDurTime = m_RsvMvDurTime;
            //m_isPickMvOnOff = true;
            //m_AddTimeCount = 0.0;
            ////---길찾기를 이용하지 않고 예약 이동 처리를 할 때 코드

            MySetAnim(AnimState.move);

            return;
        }

        //Skill상태일때는 Skill상태로 끝나야 하고 
        //Attack상태일대는 Attack상태로 끝나야 하고 
        //상태는 Skill인데 Attack애니메이션 끝이 들어온 경우라면 제외시켜버린다.
        if (m_prevState.ToString() == AnimState.skill.ToString() &&
            Type == AnimState.attack.ToString()) 
            return;

        if (m_prevState.ToString() == AnimState.attack.ToString() &&
            Type == AnimState.skill.ToString()) 
            return;

        if (IsTargetEnemyActive(0.2f) == true) //공격 거리 안에 타겟이 있느냐?
        {
            ////공격 애니메이션으로 끝난 경우 자동 공격 애니메이션을 하게 하고 싶은 경우
            //if (m_prevState.ToString() == AnimState.attack.ToString())
            //{
            //    //공격으로 끝났으면 공격 애니메이션을 취소하고 공격을 다시 시도한다.
            //    if (!string.IsNullOrEmpty(m_prevState) && m_RefAnimator != null)
            //    {
            //        m_RefAnimator.ResetTrigger(m_prevState.ToString());
            //    }
            //    m_prevState = null; //강제 어택이 들어가도록...
            //}//if (m_prevState.ToString() == AnimState.attack.ToString())
            //MySetAnim(AnimState.attack);  //다시 공격 애니

            MySetAnim(AnimState.reAttack);  //다시 공격 애니
            m_CurState = AnimState.reAttack;

            ClearMsPickPath();
        }//if (IsTargetEnemyActive() == true)
        else
        {
            MySetAnim(AnimState.idle);
        }
    }

    float a_CacRotSpeed = 0.0f;
    float a_CacRate = 0.0f;
    public void AttackRotUpdate() //공격애니메이션 중일 때 타겟을 향해 회전하게 하는 함수
    {
        //마우스 피킹을 시도했고 이동 중이면 타겟을 다시 잡지 않는다.
        if (m_isPickMvOnOff == true) 
            return;

        //보간 때문에 정확히 정밀한 공격 애니메이션만 하고 있을 때만...
        if (ISAttack() == false || IsAttAniData() == false) //공격 애니메이션이 아니면...
            return; //회전할 필요가 없다.

        FindEnemyTarget();

        a_CacRotSpeed = m_RotSpeed * 3.0f;           //초당 회전 속도

        if (IsTargetEnemyActive(1.0f) == true) 
        {  //타겟이 살아있고 공격 거리 안쪽에 있을 때만 회전 필요

            //--- 회전 허용 여부 판단 코드
            a_CacRate = animaterStateInfo.normalizedTime - 
                    (float)((int)animaterStateInfo.normalizedTime);
            //내가 공격 애니메이션 진행 중인 상태에서 몬스터가 내 공격거리 안쪽으로 들어온 경우
            if (a_CacRate <= 0.3f)  //애니메이션 앞부분부터 회전했다면 허용해 주자
            {
                m_AttRotPermit = true;
            }
            else
            {
                //공격 애니메이션 진행된 30% 이후부터는 회전 하려고 시도하면 허용하지 않겠다는 뜻
                if (m_AttRotPermit == false) 
                    return;
            }
            //--- 회전 허용 여부 판단 코드

            a_CacTgVec = m_TargetUnit.transform.position - transform.position;
            a_CacTgVec.y = 0.0f;

            //캐릭터 스프링 회전   
            a_CacAtDir = a_CacTgVec.normalized;
            if (0.0001f < a_CacAtDir.magnitude)
            {
                Quaternion a_TargetRot = Quaternion.LookRotation(a_CacAtDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, a_TargetRot,
                                        Time.deltaTime * a_CacRotSpeed);
            }
        }//if (IsTargetEnemyActive(1.0f) == true)
    }//public void AttackRotUpdate()

    bool IsTargetEnemyActive(float a_ExtLen = 0.0f)
    {
        if (m_TargetUnit != null)//타겟이 존재한다고 하더라도...
        {
            if (m_TargetUnit.activeSelf == false)
            {
                m_TargetUnit = null;
                return false;
            }

            //isDie 죽어 있어도
            MonsterCtrl a_Unit = m_TargetUnit.GetComponent<MonsterCtrl>();
            if (a_Unit.MonState == AnimState.die)
            {
                m_TargetUnit = null;
                return false;
            }

            a_CacTgVec = m_TargetUnit.transform.position - transform.position;
            a_CacTgVec.y = 0.0f;
            if (m_AttackDist + a_ExtLen < a_CacTgVec.magnitude)  //공격거리 바깥쪽에 있을 경우도 타겟을 무효화 해 버린다.
            {
                //m_TargetUnit = null; //원거리라도 타겟을 공격할 수 있으니까...
                return false;
            }

            return true;
        }//if (m_TargetUnit != null)//타겟이 존재한다고 하더라도...

        return false;
    }// bool IsTargetEnemyActive(float a_ExtLen = 0.0f)

    public bool IsAttAniData()
    {
        if (m_RefAnimator != null)
        {
            //첫번째 레이어
            animaterStateInfo = m_RefAnimator.GetCurrentAnimatorStateInfo(0);
            if (animaterStateInfo.IsName(anim.Attack1.name) == true ||
                animaterStateInfo.IsName(anim.Skill1.name) == true)
            {
                return true;
            }
        }

        return false;
    }

    void Event_AttDamage(string Type)
    {
        m_EnemyList = GameObject.FindGameObjectsWithTag("Enemy");
        iCount = m_EnemyList.Length;

        if (Type == AnimState.attack.ToString()) //공격 애니메이션이면...
        {
            //---------주변 모든 몬스터를 찾아서 데이지를 준다.(범위공격)
            for (int i = 0; i < iCount; ++i)
            {
                a_CacTgVec = m_EnemyList[i].transform.position - transform.position;
                a_fCacLen = a_CacTgVec.magnitude;
                a_CacTgVec.y = 0.0f;
                //공격각도 안에 있는 경우
                //70도 정도 //-0.7f) //-45도를 넘는 범위에 있다는 뜻.
                if (Vector3.Dot(transform.forward, a_CacTgVec.normalized) < 0.45f)
                    continue;

                //공격 범위 밖에 있는 경우
                if (m_AttackDist + 0.1f < a_fCacLen)
                    continue;

                a_EffObj = EffectPool.Inst.GetEffectObj("FX_Hit_01",
                                                Vector3.zero, Quaternion.identity);
                a_EffPos = m_EnemyList[i].transform.position;
                a_EffPos.y += 1.1f; 
                a_EffObj.transform.position = a_EffPos + (-a_CacTgVec.normalized * 1.13f);
                a_EffObj.transform.LookAt(a_EffPos + (a_CacTgVec.normalized * 2.0f));
                m_EnemyList[i].GetComponent<MonsterCtrl>().TakeDamage(this.gameObject,
                                                            GlobalValue.g_UserAttackRate);

            }//for (int i = 0; i < iCount; ++i)
            //---------주변 모든 몬스터를 찾아서 데이지를 준다.(범위공격)
        }//if (Type == AnimState.attack.ToString())
        else if (Type == AnimState.skill.ToString())
        {
            a_EffObj = EffectPool.Inst.GetEffectObj("FX_AttackCritical_01",
                                           Vector3.zero, Quaternion.identity);
            a_EffPos = transform.position;
            a_EffPos.y = a_EffPos.y + 1.0f;
            a_EffObj.transform.position = a_EffPos + (transform.forward * 2.3f);
            a_EffObj.transform.LookAt(a_EffPos + (-transform.forward * 2.0f));  

            //---------주변 모든 몬스터를 찾아서 데이지를 준다.(범위공격)
            for (int i = 0; i < iCount; ++i)
            {
                a_CacTgVec = m_EnemyList[i].transform.position - transform.position;
                a_CacTgVec.y = 0.0f;

                ////공격각도 안에 있는 경우
                ////-45도를 넘는 범위에 있다는 뜻
                //if (Vector3.Dot(transform.forward, a_CacTgVec.normalized) < -0.7f) 
                //    continue;

                //공격 범위 밖에 있는 경우
                if (m_AttackDist + 0.1f < a_CacTgVec.magnitude)
                    continue;

                a_EffObj = EffectPool.Inst.GetEffectObj("FX_Hit_01",
                                        Vector3.zero, Quaternion.identity);
                a_EffPos = m_EnemyList[i].transform.position;
                a_EffPos.y += 1.1f; //1.0f;
                a_EffObj.transform.position = a_EffPos + (-a_CacTgVec.normalized * 1.13f); //0.7f);
                a_EffObj.transform.LookAt(a_EffPos + (a_CacTgVec.normalized * 2.0f));
                m_EnemyList[i].GetComponent<MonsterCtrl>().TakeDamage(this.gameObject, GlobalValue.g_SkillAttackRate);

            }//for (int i = 0; i < iCount; ++i)
            //---------주변 모든 몬스터를 찾아서 데이지를 준다.(범위공격)
        }// else if(Type == AnimState.skill.ToString())

    }//void Event_AttDamage(string Type)

    public void TakeDamage(GameObject a_Attacker = null, float a_Damage = 10.0f)
    {
        if (Curhp <= 0.0f)
            return;

        if (pv.IsMine == true)
        { //실제 데미지는 IsMine일 때만 깎아주다 IsMine이 아니면 연출만... 
            Curhp -= a_Damage;
            if (Curhp < 0.0f)
                Curhp = 0.0f;
        }

        SetAttachColor();

        if (pv.IsMine == true)
        { //실제 데미지는 IsMine일 때만 깎아주다 IsMine이 아니면 연출만..
            //Image UI 항목의 fillAmout 속성을 조절해 생명 게이지 값 조정
            imgHpbar.fillAmount = (float)Curhp / (float)Maxhp;
        }

        GameMgr.Inst.SpawnDamageTxt((int)a_Damage, this.transform, 1);

        if (pv.IsMine == true) //사망처리도 IsMine 일때만... 나머지는 동기화로 처리함
        if (Curhp <= 0)
        {
            //Die();
        }
    }

    protected virtual void SetAttachColor()
    {
        AttachColorChange = true;
        AttachColorStartTime = Time.time;
    }

    private void FindDefShader()
    {
        if (m_SMR == null)
        {
            m_SMRList = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            m_MeshList = gameObject.GetComponentsInChildren<MeshRenderer>();
            m_SMR = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (m_SMR != null)
            {
                g_DefTexShader = m_SMR.material.shader;
            }

            if (0 < m_MeshList.Length)
            {
                g_WeaponTexShader = m_MeshList[0].material.shader;
            }

        }//if (m_SMR == null)
    }

    protected virtual void DamageColorUpdate()
    {
        FindDefShader();

        if (this.gameObject.activeSelf == false)
            return;

        if (AttachColorChange == false)
        {
            return;
        }

        a_Ratio = (Time.time - AttachColorStartTime) / AttachColorTime;
        a_Ratio = Mathf.Min(a_Ratio, 1f);
        a_fCol = a_DamageColor; 
        a_CalcColor = new Color(a_fCol, a_fCol, a_fCol);  

        if (a_Ratio >= 1f)
        {
            for (int i = 0; i < m_SMRList.Length; i++)
            {
                if (g_DefTexShader != null)
                    m_SMRList[i].material.shader = g_DefTexShader;
            }

            //------------무기
            if (m_MeshList != null)
            {
                for (int i = 0; i < m_MeshList.Length; i++)
                {
                    if (g_WeaponTexShader != null)
                        m_MeshList[i].material.shader = g_WeaponTexShader;
                }
            }
            //------------무기

            AttachColorChange = false;
        }
        else
        {
            for (int i = 0; i < m_SMRList.Length; i++)
            {
                if (GameMgr.Inst.g_AddTexShader != null
                    && m_SMRList[i].material.shader != GameMgr.Inst.g_AddTexShader)
                    m_SMRList[i].material.shader = GameMgr.Inst.g_AddTexShader;

                m_SMRList[i].material.SetColor("_AddColor", a_CalcColor);
            }

            //------------무기
            if (m_MeshList != null)
            {
                for (int i = 0; i < m_MeshList.Length; i++)
                {
                    if (GameMgr.Inst.g_AddTexShader != null
                        && m_MeshList[i].material.shader != GameMgr.Inst.g_AddTexShader)
                        m_MeshList[i].material.shader = GameMgr.Inst.g_AddTexShader;

                    m_MeshList[i].material.SetColor("_AddColor", a_CalcColor);
                }
            }
            //------------무기
        }
    }//protected virtual void DamageColorUpdate()

    public void SkillOrder(string Type, ref float Cooltime, ref float CoolType)
    {
        if (pv.IsMine == false)
            return;

        if (0.0f < CoolType)
            return;

        if (m_prevState != AnimState.skill.ToString())
        {
            MySetAnim(AnimState.skill);

            ClearMsPickPath();

            Cooltime = 7.0f;
            CoolType = Cooltime;     //쿨타임 적용
        }

    }

    void FindEnemyTarget()
    {
        //타겟의 교체는 공격거리보다는 조금 더 여유를 두고 바꾸게 한다.
        if (IsTargetEnemyActive(0.5f) == true) //살짝 거리에 따라서 오차가 발생할 수 있다.
            return;

        m_EnemyList = GameObject.FindGameObjectsWithTag("Enemy");

        float a_MinLen = float.MaxValue;
        iCount = m_EnemyList.Length;
        m_TargetUnit = null;  //우선 타겟 무효화
        for (int i = 0; i < iCount; ++i)
        {
            a_CacTgVec = m_EnemyList[i].transform.position - transform.position;
            a_CacTgVec.y = 0.0f;
            if (a_CacTgVec.magnitude <= m_AttackDist)
            {   //공격거리 안쪽에 있을 경우만 타겟으로 잡는다.
                if (a_CacTgVec.magnitude < a_MinLen)
                {
                    a_MinLen = a_CacTgVec.magnitude;
                    m_TargetUnit = m_EnemyList[i];
                }
            }//if (a_CacTgVec.magnitude < a_MinLen)
        }//for (int i = 0; i < iCount; ++i)

    }//void FindEnemyTarget()

    Vector3 a_VecLen = Vector3.zero;
    public bool MyNavCalcPath(Vector3 a_StartPos, Vector3 a_TargetPos, 
                                ref float a_PathLen) //길찾기...
    { //경로 탐색 함수
        //--- 피킹이 발생된 상황이므로 초기화 하고 계산한다.
        movePath.ClearCorners();  //경로 모두 제거 
        m_CurPathIndex = 1;       //진행 인덱스 초기화 
        m_PathEndPos = transform.position;
        //--- 피킹이 발생된 상황이므로 초기화 하고 계산한다.

        if (nvAgent == null || nvAgent.enabled == false)
            return false;

        if (NavMesh.CalculatePath(a_StartPos, a_TargetPos, -1, movePath) == false)
        {   //CalculatePath() 함수 계산이 끝나고 정상적으로instance.final 
            //즉 목적지까지 계산에 도달했다는 뜻 
            //--> p.status == UnityEngine.AI.NavMeshPathStatus.PathComplete 
            //<-- 그럴때 정상적으로 타겟으로 설정해 준다.는 뜻
            // 길 찾기 실패 했을 때 점프하는 경향이 있다. 
            return false;
        }

        if (movePath.corners.Length < 2)
            return false;

        for (int i = 1; i < movePath.corners.Length; ++i)
        {
//#if UNITY_EDITOR
//            Debug.DrawLine(movePath.corners[i - 1], movePath.corners[i], Color.cyan, 10);
//            //맨마지막 인자 duration 라인을 표시하는 시간
//            Debug.DrawLine(movePath.corners[i], movePath.corners[i] + Vector3.up * i,
//                           Color.cyan, 10);
//#endif
            a_VecLen = movePath.corners[i] - movePath.corners[i - 1];
            //a_VecLen.y = 0.0f;
            a_PathLen = a_PathLen + a_VecLen.magnitude;
        }

        if (a_PathLen <= 0.0f)
            return false;

        //-- 주인공이 마지막 위치에 도착했을 때 정확한 방향을 
        // 바라보게 하고 싶은 경우 때문에 계산해 놓는다.
        m_PathEndPos = movePath.corners[(movePath.corners.Length - 1)];

        return true;
    }

    //--- MoveToPath 관련 변수들...
    private bool a_isSucessed = true;
    private Vector3 a_CurCPos = Vector3.zero;
    private Vector3 a_CacDestV = Vector3.zero;
    private Vector3 a_TargetDir;
    private float a_CacSpeed = 0.0f;
    private float a_NowStep = 0.0f;
    private Vector3 a_Velocity = Vector3.zero;
    private Vector3 a_vTowardNom = Vector3.zero;
    private int a_OldPathCount = 0;
    ////--- MoveToPath 관련 변수들...
    public bool MoveToPath(float overSpeed = 1.0f)
    {
        a_isSucessed = true;

        if (movePath == null)
        {
            movePath = new NavMeshPath();
        }

        a_OldPathCount = m_CurPathIndex;
        if (m_CurPathIndex < movePath.corners.Length) //최소 m_CurPathIndex = 1 보다 큰 경우에는 캐릭터를 이동시켜 준다.
        {
            a_CurCPos = this.transform.position;
            a_CacDestV = movePath.corners[m_CurPathIndex];
            a_CurCPos.y = a_CacDestV.y;  //높이 오차가 있어서 도착 판정을 못하는 경우가 있다. 
            a_TargetDir = a_CacDestV - a_CurCPos;
            a_TargetDir.y = 0.0f;
            a_TargetDir.Normalize();

            a_CacSpeed = m_MoveVelocity;
            a_CacSpeed = a_CacSpeed * overSpeed;

            a_NowStep = a_CacSpeed * Time.deltaTime; //이번에 이동했을 때 이 안으로만 들어와도 무조건 도착한 것으로 본다.

            a_Velocity = a_CacSpeed * a_TargetDir;
            a_Velocity.y = 0.0f;
            nvAgent.velocity = a_Velocity;          //이동 처리...

            if ((a_CacDestV - a_CurCPos).magnitude <= a_NowStep) //중간점에 도착한 것으로 본다.  여기서 a_CurCPos == Old Position의미
            {
                movePath.corners[m_CurPathIndex] = this.transform.position;
                m_CurPathIndex = m_CurPathIndex + 1;
            }//if ((a_CacDestV - a_CurCPos).magnitude <= a_NowStep) //중간점에 도착한 것으로 본다.  

            m_AddTimeCount = m_AddTimeCount + Time.deltaTime;
            if (m_MoveDurTime <= m_AddTimeCount) //목표점에 도착한 것으로 판정한다.
            {
                m_CurPathIndex = movePath.corners.Length;
            }

        }//if (m_CurPathIndex < movePath.corners.Length) //최소 m_CurPathIndex = 1 보다 큰 경우에는 캐릭터를 이동시켜 준다.

        if (m_CurPathIndex < movePath.corners.Length)  //목적지에 아직 도착 하지 않은 경우 매 플레임 
        {
            //-------------캐릭터 회전 / 애니메이션 방향 조정
            a_vTowardNom = movePath.corners[m_CurPathIndex] - this.transform.position;
            a_vTowardNom.y = 0.0f;
            a_vTowardNom.Normalize();        // 단위 벡터를 만든다.

            if (0.0001f < a_vTowardNom.magnitude)  //로테이션에서는 모두 들어가야 한다.
            {
                Quaternion a_TargetRot = Quaternion.LookRotation(a_vTowardNom);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                                            a_TargetRot, Time.deltaTime * m_RotSpeed);
            }

            MySetAnim(AnimState.move); //"run" 애니메이션 적용
            //-------------캐릭터 회전 / 애니메이션 방향 조정
        }
        else //최종 목적지에 도착한 경우 매 플레임
        {
            if (a_OldPathCount < movePath.corners.Length) //최종 목적지에 도착한 경우 한번 발생시키기 위한 부분
            {
                ClearMsPickPath();

                if (ISAttack() == false)         //공격이 끝났을 때만 Idle로 넘어가도록...
                    MySetAnim(AnimState.idle); //idle 애니메이션 적용
            }

            a_isSucessed = false; //아직 목적지에 도착하지 않았다면 다시 잡아 줄 것이기 때문에... 
        }

        return a_isSucessed;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        //로컬 플레이어의 위치 정보 송신
        if (stream.IsWriting)
        {
            stream.SendNext(tr.position);
            stream.SendNext(tr.rotation);
            stream.SendNext(Curhp);
            stream.SendNext((int)m_CurState);
            if (m_CurState == AnimState.reAttack)
                m_CurState = AnimState.attack;
            stream.SendNext(m_DiaCount);
        }
        else //원격 플레이어의 위치 정보 수신
        {
            currPos = (Vector3)stream.ReceiveNext();
            currRot = (Quaternion)stream.ReceiveNext();
            NetHp = (float)stream.ReceiveNext();
            m_CurState = (AnimState)stream.ReceiveNext();
            m_DiaCount = (int)stream.ReceiveNext();
        }
    } //public void OnPhotonSerializeView()

    void Remote_Animation() //원격지 컴퓨터에서 애니메이션 동기화 함수
    {
        MySetAnim(m_CurState);
    }

    void Remote_TakeDamage() //원격지 컴퓨터에서 Hp 동기화 함수
    {
        if (0.0f < Curhp)
        {
            Curhp = NetHp;

            //Image UI 항목의 fillAmout 속성을 조절해 생명 게이지 값 조정
            imgHpbar.fillAmount = (float)Curhp / (float)Maxhp;

            if (Curhp <= 0) //사망처리 한번만 호출되게 하기 위하여...
            {
                //Die();
            }
        }//if (0.0f < Curhp)
    }//void Remote_TakeDamage()

    [PunRPC]
    public void TakeItemRPC(int a_ItemType)
    {
        if (pv.IsMine == false) //자기 조종하고 있는 Player 기준으로만 처리해 준다.
            return;

        ItemType a_ItemKind = (ItemType)a_ItemType;
        m_DiaCount++;

        GameMgr.Inst.m_DiaCount_Txt.text = "x " + m_DiaCount.ToString();

    }//public void TakeItemRPC(int a_ItemType)

}//public class Hero_Ctrl
