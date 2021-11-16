using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; //C# 시간관리사용하기위해 추가

public class EffectPoolUnit : MonoBehaviour
{
    public float m_delay = 1f; //풀에환원되고 적어도1초 지난것들 사용해야됨
    DateTime m_inactiveTime;   //Active 껐을때의 시간 꺼졌을때부터 1초지난거 체크위해 사용
    EffectPool m_objectPool;
    string m_effectName;

    //------------------- ParticleAutoDestroy 를 위해 필요한 부분
    public enum DESTROY_TYPE
    {
        Destroy,
        Inactive,
    }

    DESTROY_TYPE m_destroy = DESTROY_TYPE.Inactive; //풀에 환원하는게 원칙이라 기본값 Inactive
    float m_lifeTime = 0.0f;
    //안꺼지고 Loop도는 파티클들때문에 안꺼지는 파티클을 제어하기위해 
    //LifeTime설정 버프 (지속시간) 같은거에서 사용
    //LifeTime이있다면 이 변수로 제어  없다면 그냥 isPlaying으로 제어
    float m_curLifeTime;
    ParticleSystem[] m_particles;

    // Start is called before the first frame update
    void Start()
    {
        m_particles = GetComponentsInChildren<ParticleSystem>();
    }

    // Update is called once per frame
    void Update()
    {
        if (m_lifeTime > 0) //Inactive 경우인데 Pool과 연동해야됨
        {
            m_curLifeTime += Time.deltaTime;
            if (m_curLifeTime >= m_lifeTime)
            {
                DestroyParticles();
                m_curLifeTime = 0f;
            }
        }
        else
        {
            bool isPlay = false;
            for (int i = 0; i < m_particles.Length; i++)
            {
                if (m_particles[i].isPlaying) // 파티클 재생중인지 체크가능
                {
                    isPlay = true;
                    break;
                }
            }
            if (!isPlay)
            {
                DestroyParticles();
            }
        }
    }//void Update()

    void DestroyParticles()
    {
        switch (m_destroy)
        {
            case DESTROY_TYPE.Destroy:
                Destroy(gameObject);
                break;
            case DESTROY_TYPE.Inactive:
                gameObject.SetActive(false);
                break;
        }
    }
    //------------------- ParticleAutoDestroy 를 위해 필요한 부분

    public void SetObjectPool(string effectName, EffectPool objectPool)
    {
        m_effectName = effectName; //어떤이팩트
        m_objectPool = objectPool; //어떤풀에서관리하는 이팩트
        ResetParent();
    }

    public bool IsReady()
    {
        if (!gameObject.activeSelf) //꺼져있으면 풀에 들어가있음을의미
        {
            TimeSpan timeSpan = DateTime.Now - m_inactiveTime; //현재시간 - 엑티브 껏을때 시간 //timeSpan으로 값이나옴
            if (timeSpan.TotalSeconds > m_delay)  //시간을 전체 시 / 분 / 초 로 나눠서 받을수있다. 
            {   //timeSpan.TotalSeconds 초로만 반환을 했을때 1초보다 크면

                //엑티브가 꺼진지 1초이상 지나면 이펙트 여러개터트려도 문제가 발생안되서 1초 조건 걸음
                return true;
            }
        }
        return false;
    }

    public void ResetParent()
    {
        transform.SetParent(m_objectPool.transform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void OnDisable()
    {
        m_inactiveTime = DateTime.Now;
        m_objectPool.AddPoolUnit(m_effectName, this); //액티브가 꺼질 때 메모리풀에 다시 넣어줌
    }
}