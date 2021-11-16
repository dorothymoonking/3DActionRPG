using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterEvent : MonoBehaviour
{
    MonsterCtrl m_RefMonCS;

    // Start is called before the first frame update
    void Start()
    {
        m_RefMonCS = transform.parent.GetComponent<MonsterCtrl>();
    }

    //// Update is called once per frame
    //void Update()
    //{

    //}

    void Event_AttDamage(string Type)
    {
        m_RefMonCS.Event_AttDamage(Type);
    }
}
