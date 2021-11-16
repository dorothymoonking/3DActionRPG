using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public enum ItemType
{
    Sword,
    Staff,
    Bow,
    Shield,
    Diamond,
    HealPotion,
    Count
}

public class ItemClass
{
    string m_ItemName = "";
    string[] m_ItemNotice = { "캐릭터의 공격력을 증가 시켜준다.",
                                "캐릭터의 스킬 데미지를 증가 시켜준다.",
                                    "캐릭터의 공격력을 대폭 증가 시켜준다.",
                                        "캐릭터의 방여력을 증가 시켜 준다."};
    int m_ItemLevel = 0;
    float m_ItemRate = 0.0f;
    ItemType m_ItemType = ItemType.Sword;

    public void CreateItem(ItemType _type , int _level)
    {
        m_ItemType = _type;
        m_ItemName = _type.ToString();
        m_ItemLevel = _level;
        m_ItemRate = _level * 1.0f;
    }
}

public class Item_Ctrl : MonoBehaviourPunCallbacks, IPunObservable
{
    PhotonView pv = null;
    private int m_TakeHeroId = -1;
    ItemType m_ItemType = ItemType.Diamond;
    float m_ItemTime = 5.0f;
    [HideInInspector] public ItemClass m_ItemInfo = new ItemClass();
    // Start is called before the first frame update
    void Start()
    {
        //PhotonView 컴포넌트 할당
        pv = GetComponent<PhotonView>();
        pv.ObservedComponents[0] = this;
    }

    // Update is called once per frame
    void Update()
    {
        if (m_ItemType == ItemType.Diamond)
            transform.Rotate(Vector3.up, 150.0f * Time.deltaTime);

        if(0.0f < m_ItemTime)
        {
            m_ItemTime -= Time.deltaTime;
            if(m_ItemTime < 0.0f)
            {
                PhotonNetwork.Destroy(this.gameObject); //즉시 삭제 중계
            }
        }

        //누군가 이미 획득했는데 아직도 이 아이템이 삭제되지 않았다면...
        if (pv != null && pv.IsMine == true && 0 <= m_TakeHeroId)
            PhotonNetwork.Destroy(this.gameObject); //즉시 삭제 중계
    }

    void OnTriggerEnter(Collider other)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (pv == null)
            return;

        if (pv.IsMine == false)
            return;

        if (0 <= m_TakeHeroId)
            return;           //제일 먼저 전송된 Player가 먹겠지...
        //로컬에서 한번 먹었으면 두번 먹지 못하게 로컬 변수를 만들어 막는 거 고려한다.

        if (other.tag == "Player") 
        {
            Hero_Ctrl a_refHero = other.gameObject.GetComponent<Hero_Ctrl>();
            if (a_refHero != null)
            {
                a_refHero.pv.RPC("TakeItemRPC", RpcTarget.AllViaServer, (int)m_ItemType);

                m_TakeHeroId = a_refHero.pv.Owner.ActorNumber;
                //먹은 유저에게 보상을 한번만 지급

            }//if (a_refHero != null)

            PhotonNetwork.Destroy(this.gameObject); //즉시 삭제 중계 하는 것이 더 안전할 것으로 판단됨
        }//if (other.tag == "Player") 
    } //void OnTriggerEnter(Collider other)

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        ////로컬 플레이어의 위치 정보 송신
        if (stream.IsWriting)
        {
            stream.SendNext(m_TakeHeroId);
        }
        else //원격 플레이어의 위치 정보 수신
        {
            m_TakeHeroId = (int)stream.ReceiveNext();
        }
    }
}
