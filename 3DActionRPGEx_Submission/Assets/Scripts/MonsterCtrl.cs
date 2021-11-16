using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Photon.Pun;

public enum MonType
{
    Skeleton,
    Alien    
}

//에어리언의 애니메이터는 절대강좌의 하드코딩을 따라해서 어쩔 수 없이 Photon Animator View를 사용했다.
//현상은 에어리언이 공격 애니메이션 중인데 중간에 게임에 들어온 유저의 입장에서는 숨쉬기만 하고 있었다.

public class MonsterCtrl : MonoBehaviourPunCallbacks, IPunObservable
{
    //------------------------ HP바 표시
    float Curhp = 100;
    float Maxhp = 100;
    float NetHp = 100;      //CurHP 중계용(Update()함수에서 죽는 연출을 한번만 실행 시켜주기 위해서...)
    public Image imgHpbar;  //using UnityEngine.UI;
    //------------------------ HP바 표시

    float m_MonAttackRate = 10.0f;

    //인스펙터뷰에 표시할 애니메이션 클래스 변수
    public Anim anim;  //AnimSupporter.cs 쪽에 정의되어 있음

    //몬스터의 현재 상태 정보를 저장할 Enum 변수
    public AnimState MonState = AnimState.idle;  //AnimSupporter.cs 쪽에 정의되어 있음

    //public MonType m_MonType = MonType.Skeleton;
    Animation m_RefAnimation = null; //Skeleton
    Animator  m_RefAnimator = null;  //Alien
    AnimatorStateInfo animaterStateInfo;

    //----MonsterAI
    [HideInInspector] public GameObject m_AggroTarget = null;
    int m_AggroTgID = -1;               //이 몬스터가 공격해야할 캐럭터의 고유번호
    Vector3 m_MoveDir = Vector3.zero;   //수평 진행 노멀 방향 벡터
    Vector3 m_CacVLen = Vector3.zero;   //주인공을 향하는 벡터
    float  a_CacDist  = 0.0f;           //거리 계산용 변수
    float  traceDist  = 7.0f;           //추적 거리
    float  attackDist = 1.8f;           //공격 거리
    Quaternion a_TargetRot;             //회전 계산용 변수
    float m_RotSpeed = 7.0f;            //초당 회전 속도
    float m_NowStep = 0.0f;                //이동 계산용 변수
    Vector3 a_MoveNextStep = Vector3.zero; //이동 계산용 변수
    float m_MoveVelocity = 2.0f;        //평면 초당 이동 속도...
    GameObject m_Attacker = null;       //이 몬스터를 공격한 캐릭터 (사망 연출시 넉백 방향 계산용)
    //----MonsterAI

    //--------죽는 연출
    protected Vector3 m_DieDir = Vector3.zero;
    protected float m_DieDur = 0.0f;
    protected float m_DieTimer = 0.0f;
    //--------죽는 연출

    //-------------데미지 칼라 관련 변수
    protected SkinnedMeshRenderer m_SMR = null;
    protected SkinnedMeshRenderer[] m_SMRList = null;
    protected MeshRenderer[] m_MeshList = null;          //장착 무기
    protected float a_Ratio = 0.0f;
    protected Color a_CalcColor;

    private Shader  g_DefTexShader = null;
    private Shader  g_WeaponTexShader = null;
    //-------------데미지 칼라 관련 변수

    //---- Navigation
    protected NavMeshAgent nvAgent;    //using UnityEngine.AI;
    protected NavMeshPath movePath;

    protected Vector3 m_PathEndPos = Vector3.zero;
    int m_CurPathIndex = 1;
    protected double m_MoveDurTime = 0.0;     //목표점까지 도착하는데 걸리는 시간
    protected double m_AddTimeCount = 0.0;    //누적시간 카운트 
    float m_MoveTick = 0.0f;
    //---- Navigation

    //------------- 네트웍 동기화를 위하여...
    PhotonView pv = null;
    Transform tr;

    Vector3     currPos = Vector3.zero;
    Quaternion  currRot = Quaternion.identity;
    int currAnim = 0;
    //------------- 네트웍 동기화를 위하여...

    float m_IsOnece = 0.02f;

    [HideInInspector] public int m_MonIndexNum;
    //호출순서 InstantiateRoomObject() -> Awake() -> OnPhotonSerializeView() -> Start()
    void Awake()
    {
        //PhotonView 컴포넌트 할당
        pv = GetComponent<PhotonView>();
        pv.ObservedComponents[0] = this;   
        if(2 <= pv.ObservedComponents.Count)
            pv.ObservedComponents[1] = GetComponentInChildren<PhotonAnimatorView>();

        tr = GetComponent<Transform>();

        //------------OnPhotonSerializeView()에서 동기화를 받을 변수들은 이쪽에서 초기화 한다. 호출순서 때문에...
        //원격 캐릭터의 위치 및 회전 값을 처리할 변수의 초깃값 설정
        currPos = tr.position;
        currRot = tr.rotation;

        Maxhp = 100 * ((int)GlobalValue.g_GameDifficulty + 1);
        Curhp = Maxhp;
        NetHp = Maxhp;
        //------------OnPhotonSerializeView()에서 동기화를 받을 변수들은 이쪽에서 초기화 한다. 호출순서 때문에...
    }

    // Start is called before the first frame update
    void Start()
    {
        m_RefAnimation = GetComponentInChildren<Animation>();  //자신의 게임오브젝트 하위 게임오브젝트 중에서 Animation 찾게된 첫번째 Animation 컴포넌트를 리턴
        m_RefAnimator = GetComponentInChildren<Animator>();

        movePath = new NavMeshPath();
        nvAgent = this.gameObject.GetComponent<NavMeshAgent>();
        nvAgent.updateRotation = false;

        FindDefShader();
    }

    // Update is called once per frame
    void Update()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (MonState == AnimState.die)
            return;

        m_MoveTick = m_MoveTick - Time.deltaTime;
        if (m_MoveTick < 0.0f)
            m_MoveTick = 0.0f;

        if (pv.IsMine == true)
        {
            MonStateUpdate();
            MonActionUpdate();
        }
        else
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

            Remote_TrUpdate();
            Remote_TakeDamage();
            Remote_Animation();
        }
    }

    //일정한 간격으로 몬스터의 행동 상태를 체크하고 monsterState 값 변경
    void MonStateUpdate()
    {
        if (m_AggroTarget != null)      //어그로 타겟이 있을 경우
        {
            m_CacVLen = m_AggroTarget.transform.position - this.transform.position;

            m_CacVLen.y = 0.0f;
            m_MoveDir = m_CacVLen.normalized; //주인공을 향해 바라 보도록...
            a_CacDist = m_CacVLen.magnitude;

            //--- 직선거리가 아니고 길찾기로 이동중 이면
            if (2 < movePath.corners.Length) //길찾기로 이동 중일 때 
                traceDist = 14.0f; //추적거리
            else                             //일반 이동일 때
                traceDist = 7.0f;  //추적거리
            //--- 직선거리가 아니고 길찾기로 이동중 이면

            if (a_CacDist <= attackDist) //공격거리 범위 이내로 들어왔는지 확인
            {
                MonState = AnimState.attack;
            }
            else if (a_CacDist <= traceDist) //추적거리 범위 이내로 들어왔는지 확인
            {
                MonState = AnimState.trace;  //몬스터의 상태를 추적으로 설정
            }
            else
            {
                MonState = AnimState.idle;   //몬스터의 상태를 idle 모드로 설정
                m_AggroTarget = null;
                m_AggroTgID = -1;
            }

        }//if (m_AggroTarget != null)
        else //if (m_AggroTarget == null)
        {
            GameObject[] a_players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < a_players.Length; i++)
            {
                m_CacVLen = a_players[i].transform.position - this.transform.position;
                m_CacVLen.y = 0.0f;
                m_MoveDir = m_CacVLen.normalized; //주인공을 향해 바라 보도록...
                a_CacDist = m_CacVLen.magnitude;

                //--- 직선거리가 아니고 길찾기로 이동중 이면
                if (2 < movePath.corners.Length) //길찾기로 이동 중일 때 
                    traceDist = 14.0f; //추적거리
                else                             //일반 이동일 때
                    traceDist = 7.0f;  //추적거리
                //--- 직선거리가 아니고 길찾기로 이동중 이면

                if (a_CacDist <= attackDist) //공격거리 범위 이내로 들어왔는지 확인
                {
                    MonState = AnimState.attack;
                    m_AggroTarget = a_players[i].gameObject;  //타겟설정
                    m_AggroTgID = a_players[i].GetComponent<Hero_Ctrl>().pv.ViewID;
                    break;
                }
                else if (a_CacDist <= traceDist) //추적거리 범위 이내로 들어왔는지 확인
                {
                    MonState = AnimState.trace; //몬스터의 상태를 추적으로 설정
                    m_AggroTarget = a_players[i].gameObject;  //타겟설정
                    m_AggroTgID = a_players[i].GetComponent<Hero_Ctrl>().pv.ViewID;
                    break;
                }
            }//for (int i = 0; i < a_players.Length; i++)
        } //if (m_AggroTarget == null)

    }//void MonStateUpdate()

    void MonActionUpdate()
    {
        if (MonState == AnimState.attack) //공격상태 일 때
        {//아직 공격 애니메이션 중이라면...
            if (m_AggroTarget == null)
            {
                MySetAnim(anim.Idle.name, 0.13f); //애니메이션 적용
                return;
            }

            if (0.0001f < m_MoveDir.magnitude)
            {
                a_TargetRot = Quaternion.LookRotation(m_MoveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                                        a_TargetRot, Time.deltaTime * m_RotSpeed);
            }

            MySetAnim(anim.Attack1.name, 0.12f); //애니메이션 적용
            ClearMsPickPath(); //이동 즉시 취소
        }//if (MonState == AnimState.attack) //공격상태 일 때
        else if (MonState == AnimState.trace)
        {
            if (m_AggroTarget == null)
            {
                MySetAnim(anim.Idle.name, 0.13f); //애니메이션 적용
                return;
            }

            if (IsAttackAnim() == true)
            {//아직 공격 애니메이션 중이라면 공격 애니가 끝난 경우에만 추적 이동하도록...
                return;
            }

            //---------------- 네비게이션 메시를 이용한 이동 방법            
            if (m_MoveTick <= 0.0f) //----------- 길찾기를 하는 이동
            {
                //주기적으로 피킹
                float a_PathLen = 0.0f;
                if (MyNavCalcPath(this.transform.position, 
                            m_AggroTarget.transform.position, ref a_PathLen) == true)
                {
                    m_MoveDurTime = a_PathLen / m_MoveVelocity; //도착하는데 걸리는 시간
                    m_AddTimeCount = 0.0;

                }//if (a_IsPathOK == true)
                m_MoveTick = 0.2f;
            }

            MoveToPath(); //길찾기를 하는 이동 처리
            //---------------- 네비게이션 메시를 이용한 이동 방법

            //////----------- 길찾기를 안했을 때 이동시 회전 시켜준다.
            //if (0.0001f < m_MoveDir.magnitude)
            //{
            //    a_TargetRot = Quaternion.LookRotation(m_MoveDir);
            //    transform.rotation = Quaternion.Slerp(transform.rotation,
            //                            a_TargetRot, Time.deltaTime * m_RotSpeed);
            //}
            //////----------- 길찾기를 안했을 때 이동시 회전 시켜준다.

            ////------------- 일반 이동 코드
            //m_NowStep = m_MoveVelocity * Time.deltaTime; //한걸음 크기
            //a_MoveNextStep = m_MoveDir * m_NowStep;      //한걸음 벡터
            //a_MoveNextStep.y = 0.0f;

            //this.transform.position = this.transform.position + a_MoveNextStep;
            ////------------- 일반 이동 코드

            ////----------- 길찾기를 안했을 때 이동 계산
            //MySetAnim(anim.Move.name, 0.12f);  //애니메이션 적용
            ////----------- 길찾기를 안했을 때 이동 계산

        }//else if (MonState == AnimState.trace)
        else if (MonState == AnimState.idle)
        {
            MySetAnim(anim.Idle.name, 0.13f); //애니메이션 적용
        }

    }//void MonActionUpdate()

    public void MySetAnim(string newAnim, float CrossTime = 0.0f)
    {
        if (m_RefAnimation != null)
        {
            if (0.0f < CrossTime)
                m_RefAnimation.CrossFade(newAnim, CrossTime);
            else
                m_RefAnimation.Play(newAnim);
 
        }//if (m_RefAnimation != null)

        if (m_RefAnimator != null)
        {
            if (newAnim == anim.Move.name)
            {
                if (m_RefAnimator.GetBool("IsAttack") == true)
                    m_RefAnimator.SetBool("IsAttack", false);

                if (m_RefAnimator.GetBool("IsRun") == false)
                    m_RefAnimator.SetBool("IsRun", true);

                //m_RefAnimator.Play(anim.Move.name);  
                //모든 애니메이션을 취소하고 즉시 run을 발동시키는 함수
            }
            else if (newAnim == anim.Idle.name)
            {
                if (m_RefAnimator.GetBool("IsAttack") == true)
                    m_RefAnimator.SetBool("IsAttack", false);

                if (m_RefAnimator.GetBool("IsRun") == true)
                    m_RefAnimator.SetBool("IsRun", false);
            }
            else if (newAnim == anim.Attack1.name)
            {
                if (m_RefAnimator.GetBool("IsAttack") == false)
                    m_RefAnimator.SetBool("IsAttack", true);
            }
            if (newAnim == anim.Die.name)
            {
                //CrossFade상태에서는 이것보다는 위의 if (m_CurAniState == newAnim) 더 정확하다.
                animaterStateInfo = m_RefAnimator.GetCurrentAnimatorStateInfo(0);  
                if (animaterStateInfo.IsName(anim.Die.name) == false)
                { //현재 재생중인 애니메이션 상태가 Die 이 아니라면... 이라는 뜻
                    m_RefAnimator.CrossFade(anim.Die.name, CrossTime);
                    //m_RefAnimator.speed = 1.0f;  //원래 속도로...
                }
            }
        }//if (m_RefAnimator != null)

        if (pv.IsMine == true)
        {
            if (newAnim == anim.Idle.name)
                currAnim = 0;
            else if (newAnim == anim.Move.name)
                currAnim = 1;
            else if (newAnim == anim.Attack1.name)
                currAnim = 2;
            else if (newAnim == anim.Die.name)
                currAnim = 3;
        }//if (pv.IsMine == true)
    }

    //-------------공격 애니 관련 변수
    float a_CacRate = 0.0f;
    float a_NormalTime = 0.0f;
    //-------------공격 애니 관련 변수
    public bool IsAttackAnim() //공격애니메이션 상태 체크 함수
    {
        if (m_RefAnimation != null)
        {
            if (m_RefAnimation.IsPlaying(anim.Attack1.name) == true)
            {
                a_NormalTime = m_RefAnimation[anim.Attack1.name].time
                                 / m_RefAnimation[anim.Attack1.name].length;

                //m_RefAnimation["Attack1h1"].time   
                //어느 정도 플레이가 되고 있는지의 현재 시간값
                //m_RefAnimation["Attack1h1"].length 
                //한동작이 끝날 때까지의 시간값

                //소수점 한동작이 몇프로 진행되었는지 계산 변수
                a_CacRate = a_NormalTime - (float)((int)a_NormalTime);

                if (a_CacRate < 0.95f) //공격 애니메이션 끝부분이 아닐 때(공격애니메이션 중이라는 뜻)
                    return true;
            }
        }//if (m_RefAnimation != null) 

        return false;
    }

    public void TakeDamage(GameObject a_Attacker, float a_Damage = 10.0f)
    {
        if (Curhp <= 0.0f)
            return;

        m_Attacker = a_Attacker;

        if (pv.IsMine == true) //실제 데미지는 IsMine일 때만 깎아주다 IsMine이 아니면 연출만...
        {
            Curhp -= a_Damage;
            if (Curhp < 0.0f)
                Curhp = 0.0f;

            //Image UI 항목의 fillAmout 속성을 조절해 생명 게이지 값 조정
            imgHpbar.fillAmount = (float)Curhp / (float)Maxhp;

        }//if (pv.IsMine == true) 

        GameMgr.Inst.SpawnDamageTxt((int)a_Damage, this.transform);

        if (pv.IsMine == true) //사망처리도 IsMine 일때만... 나머지는 동기화로 처리함
        if (Curhp <= 0)  //사망 처리
        {
            MonState = AnimState.die;
            MySetAnim(anim.Die.name, 0.1f);
            m_DieDur = 2.0f;
            m_DieTimer = 2.0f;
            m_DieDir = this.transform.position - a_Attacker.transform.position;
            m_DieDir.y = 0.0f;
            m_DieDir.Normalize();
            BoxCollider a_BoxColl = gameObject.GetComponentInChildren<BoxCollider>();
            if (a_BoxColl != null)
                a_BoxColl.enabled = false;
            //nvAgent.enabled = false;
            FindDefShader();
            StartCoroutine("DieDirect"); //죽는 연출

                //if (m_RefAnimation != null)    //해골병사
                //    GameMgr.Inst.MonsterDie();
                //else //if (m_RefAnimator != null) //에어리언
                //    GameMgr.Inst.MonsterDie(1);

            int a_Ran = Random.Range(0, 2);
            if(a_Ran == 0)
                CreateItem();

        }//if (Curhp <= 0)  //사망 처리
    }//public void TakeDamage(GameObject a_Attacker, float a_Damage = 10.0f)

    void FindDefShader()
    {
        if (m_SMR == null)
        {
            m_SMRList = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            m_MeshList = gameObject.GetComponentsInChildren<MeshRenderer>();
            m_SMR = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();

            if (m_SMR != null)
                g_DefTexShader = m_SMR.material.shader;

            if (0 < m_MeshList.Length)
                g_WeaponTexShader = m_MeshList[0].material.shader;

        }//if (m_SMR == null)
    }

    Transform a_Canvas = null;
    IEnumerator DieDirect() //몬스터 죽는 연출 
    {
        while (true)
        {
            a_Ratio = m_DieTimer / m_DieDur;
            a_Ratio = Mathf.Min(a_Ratio, 1f);
            a_CalcColor = new Color(1.0f, 1.0f, 1.0f, a_Ratio);

            ////---------------- 뒤로 밀리게...
            if (0.9f < a_Ratio && 0.0f < m_DieDir.magnitude)
            {
                transform.position = transform.position + m_DieDir
                                    * (((a_Ratio * 0.38f) * 14.0f) * Time.deltaTime);
            }
            ////---------------- 뒤로 밀리게...

            if (a_Ratio < 0.83f)
            {
                if (a_Canvas == null)
                    a_Canvas = transform.Find("Canvas");
                if (a_Canvas != null)
                    a_Canvas.gameObject.SetActive(false);
            }

            for (int i = 0; i < m_SMRList.Length; i++)
            {
                if (GameMgr.Inst.g_VertexLitShader != null
                    && m_SMRList[i].material.shader != GameMgr.Inst.g_VertexLitShader)
                {
                    m_SMRList[i].material.shader = GameMgr.Inst.g_VertexLitShader;
                }

                m_SMRList[i].material.SetColor("_Color", a_CalcColor);
            }

            //------------무기
            if (m_MeshList != null)
            {
                for (int i = 0; i < m_MeshList.Length; i++)
                {
                    if (GameMgr.Inst.g_VertexLitShader != null
                        && m_MeshList[i].material.shader != GameMgr.Inst.g_VertexLitShader)
                    {
                        m_MeshList[i].material.shader = GameMgr.Inst.g_VertexLitShader;
                    }

                    m_MeshList[i].material.SetColor("_Color", a_CalcColor);
                }
            }
            //------------무기

            m_DieTimer = m_DieTimer - Time.deltaTime;
            if (m_DieTimer < 0.0f)
            {
                if (pv.IsMine == true)
                    PhotonNetwork.Destroy(this.gameObject);
                
                //----- 몬스터 재스폰 부분
                if (PhotonNetwork.IsMasterClient == true) 
                { 
                    GameMgr.Inst.m_MonNum--;
                    GameMgr.Inst.m_SpwanPoint[m_MonIndexNum].SetActive(true);
                    GameMgr.Inst.m_ReSpwanTime[m_MonIndexNum] = 3.0f;
                }
                //----- 몬스터 재스폰 부분

                //유저가 나갔다 들어오면 죽은 몬스터가 남아 있는 경우가 있었기 때문에 
                //Destroy() 대신에  PhotonNetwork.Destroy() 사용함

                yield break;
            }

            yield return null;
        }//while (true)
    }

    Vector3 a_DistVec = Vector3.zero;
    float   a_CacLen = 0.0f;
    public void Event_AttDamage(string Type) //애니메이션 이벤트 함수로 호출
    {
        if (pv.IsMine == true)
        {
            if (m_AggroTarget == null)
                return;

            a_DistVec = m_AggroTarget.transform.position - transform.position;
            a_CacLen = a_DistVec.magnitude;
            a_DistVec.y = 0.0f;
            //공격각도 안에 있는 경우
            if (Vector3.Dot(transform.forward, a_DistVec.normalized) < 0.0f) //90도를 넘는 범위에 있다는 뜻
                return;

            //공격 범위 밖에 있는 경우
            if ((attackDist + 1.7f) < a_CacLen)
                return;

            if (m_RefAnimator != null)  //if (m_MonType == MonType.MT_ALIEN)
            {
                if (((int)GlobalValue.g_GameDifficulty + 1) > 1)
                    m_MonAttackRate = 5.0f + ((float)GlobalValue.g_GameDifficulty + 1);
                else
                    m_MonAttackRate = 5.0f;

                m_AggroTarget.GetComponent<Hero_Ctrl>().TakeDamage(null, m_MonAttackRate);
            }
            else
            {
                if (((int)GlobalValue.g_GameDifficulty + 1) > 1)
                    m_MonAttackRate = 10.0f + ((float)GlobalValue.g_GameDifficulty + 1);
                else
                    m_MonAttackRate = 10.0f;

                m_AggroTarget.GetComponent<Hero_Ctrl>().TakeDamage(null, m_MonAttackRate);
            }

        }//if (pv.IsMine == true)
        else
        {
            if (m_AggroTgID < 0)
                return;

            GameObject a_AggroTg = null;
            GameObject[] a_players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < a_players.Length; i++)
            {
                if (m_AggroTgID == a_players[i].GetComponent<Hero_Ctrl>().pv.ViewID)
                {
                    a_AggroTg = a_players[i];
                    break;
                }
            }//for (int i = 0; i < a_players.Length; i++)

            if (a_AggroTg == null)
                return;

            a_DistVec = a_AggroTg.transform.position - transform.position;
            a_CacLen = a_DistVec.magnitude;
            a_DistVec.y = 0.0f;
            //공격각도 안에 있는 경우
            if (Vector3.Dot(transform.forward, a_DistVec.normalized) < 0.0f) //90도를 넘는 범위에 있다는 뜻
                return;

            //공격 범위 밖에 있는 경우
            if ((attackDist + 1.7f) < a_CacLen) 
                return;

            if (m_RefAnimator != null)  //if (m_MonType == MonType.MT_ALIEN)
                a_AggroTg.GetComponent<Hero_Ctrl>().TakeDamage(null, 5); //이쪽은 연출만... 
            else
                a_AggroTg.GetComponent<Hero_Ctrl>().TakeDamage(null, 10); //이쪽은 연출만...

        }// else

    } //public void Event_AttDamage(string Type)


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

            MySetAnim(anim.Move.name, 0.12f);
            //-------------캐릭터 회전 / 애니메이션 방향 조정
        }
        else //최종 목적지에 도착한 경우 매 플레임
        {
            if (a_OldPathCount < movePath.corners.Length) //최종 목적지에 도착한 경우 한번 발생시키기 위한 부분
                MySetAnim(anim.Idle.name, 0.13f); //idle 애니메이션 적용

            a_isSucessed = false; //아직 목적지에 도착하지 않았다면 다시 잡아 줄 것이기 때문에... 
        }

        return a_isSucessed;
    }

    void ClearMsPickPath() //마우스 픽킹이동 취소 함수
    {
        //----피킹을 위한 동기화 부분
        m_PathEndPos = transform.position;
        if (0 < movePath.corners.Length)
        {
            movePath.ClearCorners();  //경로 모두 제거 
        }
        m_CurPathIndex = 1;       //진행 인덱스 초기화
        //----피킹을 위한 동기화 부분
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        //로컬 플레이어의 위치 정보 송신
        if (stream.IsWriting)
        {
            stream.SendNext(tr.position);
            stream.SendNext(tr.rotation);
            stream.SendNext(Curhp);
            stream.SendNext(m_AggroTgID);

            stream.SendNext(currAnim); //currAnim 값은IsMine일 때만 실시간으로 바뀔 것이다.
            stream.SendNext(m_MonIndexNum);
        }
        else //원격 플레이어의 위치 정보 수신
        {
            currPos = (Vector3)stream.ReceiveNext();
            currRot = (Quaternion)stream.ReceiveNext();
            NetHp   = (float)stream.ReceiveNext();
            m_AggroTgID = (int)stream.ReceiveNext();

            currAnim = (int)stream.ReceiveNext(); //OtherPC는 원격으로 받아서 처리할 것이다.
            m_MonIndexNum = (int)stream.ReceiveNext();
        }
    }

    void Remote_TrUpdate()
    {
        if (5.0f < (tr.position - currPos).magnitude)
        {
            tr.position = currPos;
        }
        else
        {
            //원격 플레이어의 Monster를 수신받은 위치까지 부드럽게 이동시킴
            tr.position = Vector3.Lerp(tr.position, currPos, Time.deltaTime * 10.0f);
        }
        //원격 플레이어의 Monster를 수신받은 각도만큼 부트럽게 회전시킴
        tr.rotation = Quaternion.Slerp(tr.rotation, currRot, Time.deltaTime * 10.0f);
    }

    void Remote_TakeDamage() //원격지 컴퓨터에서 Hp 동기화 함수
    {
        if (0.0f < Curhp) //IsMine일때만 죽는 애니메이션을 동기화 시켜준다.
        {
            Curhp = NetHp;
            //Image UI 항목의 fillAmout 속성을 조절해 생명 게이지 값 조정
            imgHpbar.fillAmount = (float)Curhp / (float)Maxhp;

            if (Curhp <= 0.0f)
            {
                Curhp = 0.0f;

                MonState = AnimState.die;
                MySetAnim(anim.Die.name, 0.1f); //애니메이션 적용
                m_DieDur = 2.0f;
                m_DieTimer = 2.0f;
                if (m_Attacker != null)
                    m_DieDir = this.transform.position - m_Attacker.transform.position;
                else
                    m_DieDir = -this.transform.forward;
                m_DieDir.y = 0.0f;
                m_DieDir.Normalize();
                BoxCollider a_BoxColl = gameObject.GetComponentInChildren<BoxCollider>();
                if (a_BoxColl != null)
                    a_BoxColl.enabled = false;
                //nvAgent.enabled = false;
                FindDefShader();
                StartCoroutine("DieDirect"); //죽는 연출
            }
        }//if (0.0f < Curhp)   
        else
        {
            Curhp = NetHp;
            imgHpbar.fillAmount = (float)Curhp / (float)Maxhp;
        }
    }

    void Remote_Animation() //원격지 컴퓨터에서 애니메이션 동기화 함수
    {
        if (m_RefAnimator != null)  //Alien 이면...
            return;

        if (currAnim == 0)
        {
            MySetAnim(anim.Idle.name, 0.13f);
        }
        else if (currAnim == 1)
        {
            MySetAnim(anim.Move.name, 0.12f);
        }
        else if (currAnim == 2)
        {
            MySetAnim(anim.Attack1.name, 0.12f); //애니메이션 적용
        }
        else if (currAnim == 3)
        {
            MySetAnim(anim.Die.name, 0.1f);
        }
    }

    //아이템을 생성하는 함수 
    void CreateItem()
    {
        Vector3 a_HPos = tr.position;
        GameObject a_Item = null;
        Item_Ctrl a_ItemCtrl = null;
        a_HPos.y += 1.0f;
        int a_Ran = Random.Range(0, (int)ItemType.Count);
        int a_Levle = 0;

        if(a_Ran == (int)ItemType.Sword)
            a_Item = PhotonNetwork.InstantiateRoomObject("Diamond_Item", a_HPos, Quaternion.identity, 0);

        else if(a_Ran == (int)ItemType.Staff)
            a_Item = PhotonNetwork.InstantiateRoomObject("Diamond_Item", a_HPos, Quaternion.identity, 0);

        else if(a_Ran == (int)ItemType.Bow)
            a_Item = PhotonNetwork.InstantiateRoomObject("Diamond_Item", a_HPos, Quaternion.identity, 0);

        else if(a_Ran == (int)ItemType.Shield)
            a_Item = PhotonNetwork.InstantiateRoomObject("Diamond_Item", a_HPos, Quaternion.identity, 0);

        else if(a_Ran == (int)ItemType.Diamond)
            a_Item = PhotonNetwork.InstantiateRoomObject("Diamond_Item", a_HPos, Quaternion.identity, 0);

        else if(a_Ran == (int)ItemType.HealPotion)
            a_Item = PhotonNetwork.InstantiateRoomObject("Diamond_Item", a_HPos, Quaternion.identity, 0);

        if(-1 < a_Ran && a_Ran < 4) //장비 아이템이라는 뜻
        {
            int a_RanLevel = Random.Range(0, 50);
        }

        if (a_Item != null)
            a_ItemCtrl = a_Item.GetComponent<Item_Ctrl>();
        a_ItemCtrl.m_ItemInfo.CreateItem((ItemType)a_Ran, 1);
    }


} //public class MonsterCtrl
