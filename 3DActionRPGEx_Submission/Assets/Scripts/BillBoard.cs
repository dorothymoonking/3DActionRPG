using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BillBoard : MonoBehaviour
{
    Transform m_CameraTr = null;

    // Start is called before the first frame update
    void Start()
    {
        m_CameraTr = Camera.main.transform;
    }

    //// Update is called once per frame
    //void Update()
    //{
        
    //}

    void LateUpdate()
    {
        this.transform.forward = m_CameraTr.forward;  //빌보드
    }
}
