using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCtrl : MonoBehaviour
{
    private GameObject m_Player = null;
    private Vector3 m_TargetPos = Vector3.zero;

    //---- 카메라 위치 계산용 변수
    private float m_RotH = 0.0f;    //마우스 좌우 조작값 계산용 변수 
    private float m_RotV = 0.0f;    //마우스 상하 조작값 계산용 변수 
    private float hSpeed = 5.0f;    //마우스 좌우 회전에 대한 카메라 회전 스피드 설정값
    private float vSpeed = 2.4f;    //마우스 상하 회전에 대한 카메라 회전 스피드 설정값
    private float vMinLimit = -7.0f; //-7.0f;  //위 아래 각도 제한
    private float vMaxLimit = 80.0f; //80.0f;   //위 아래 각도 제한
    private float zoomSpeed = 1.0f;  //마우스 휠 조작에대한 줌인아웃 스피드 설정값
    private float maxDist = 50.0f;   //마우스 줌 아웃 최대 거리 제한값
    private float minDist = 3.0f;    //마우스 줌 인 최소 거리 제한값
    //---- 카메라 위치 계산용 변수

    //---- 주인공을 기준으로 한 상대적인 구좌표계 기준의 초기값
    private float m_DefaltRotH = 0.0f;   //평면 기준의 회전 각도
    private float m_DefaltRotV = 27.0f;  //높이 기준의 회전 각도
    private float m_DefaltDist = 5.2f;   //타겟에서 카메라까지의 거리
    //---- 주인공을 기준으로 한 상대적인 구좌표계 기준의 초기값

    //---- 계산에 필요한 변수들...
    private Quaternion a_BuffRot;
    private Vector3 a_BasicPos = Vector3.zero;
    public float distance = 17.0f;
    private Vector3 a_BuffPos;
    //---- 계산에 필요한 변수들...


    public void InitCamera(GameObject a_Player)
    {
        m_Player = a_Player;

        //Start(); //InitCamera() 함수를 Hero_Ctrl class의 Awake()함수에서 호출할 것이므로
        //여기서 Start() 함수를 호출할 필요는 없다.
    }

    // Start is called before the first frame update
    void Start()
    {
        if (m_Player == null)
            return;

        //m_Player = GameObject.Find("SK_Bei_T_pose");

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y = m_TargetPos.y + 1.4f;

        //-------카메라 위치 계산 공식 (구좌표계를 직각좌표계로 환산하는 부분)
        m_RotH = m_DefaltRotH;  //평면 기준의 회전 각도 
        m_RotV = m_DefaltRotV;  //높이 기준의 회전 각도
        distance = m_DefaltDist;

        a_BuffRot = Quaternion.Euler(m_RotV, m_RotH, 0);
        a_BasicPos.x = 0.0f;
        a_BasicPos.y = 0.0f;
        a_BasicPos.z = -distance;

        a_BuffPos = (a_BuffRot * a_BasicPos) + m_TargetPos;

        transform.position = a_BuffPos;  //<--- 카메라의 직각좌표계 기준의 위치

        transform.LookAt(m_TargetPos);
        //-------카메라 위치 계산 공식

    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (m_Player == null)
            return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y = m_TargetPos.y + 1.4f;

        if (Input.GetMouseButton(1))  //마우스 우측버튼을 누르고 있는 동안
        {
            m_RotH += Input.GetAxis("Mouse X") * hSpeed; //마우스를 좌우로 움직였을 때 값
            m_RotV -= Input.GetAxis("Mouse Y") * vSpeed; //마우스를 위아래로 움직였을 때 값

            m_RotV = ClampAngle(m_RotV, vMinLimit, vMaxLimit);
        }

        if (Input.GetAxis("Mouse ScrollWheel") < 0 && distance < maxDist)
        {
            distance += zoomSpeed;
        }

        if (Input.GetAxis("Mouse ScrollWheel") > 0 && distance > minDist)
        {
            distance -= zoomSpeed;
        }

        a_BuffRot = Quaternion.Euler(m_RotV, m_RotH, 0);
        a_BasicPos.x = 0.0f;
        a_BasicPos.y = 0.0f;
        a_BasicPos.z = -distance;

        a_BuffPos = a_BuffRot * a_BasicPos + m_TargetPos;

        transform.position = a_BuffPos;  //<--- 카메라의 직각좌표계 기준의 위치

        transform.LookAt(m_TargetPos);
    }

    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360)
            angle += 360;
        if (angle > 360)
            angle -= 360;
        return Mathf.Clamp(angle, min, max);
    }
}
