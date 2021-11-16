using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowAnimation : MonoBehaviour {

    public GameObject[] frames;
    public GameObject circle;
    public float pacing;

    private int currentFrame;
    private int previousFrame;
    [Range (0, 50)]
    public float frameOffset = 0f;
    public float frameSpeed = 10f;

    private float startFrame;
    private float timeLoop = 0f;

    public bool loop;
    float m_CacTime = 0.0f;
    int m_CurIdx = 0;
    int m_Idx = 0;

    void Start () {

        for (int i = 0; i < frames.Length; i++) { // for each object/frame disable them at start
            frames[i].SetActive (false);
        }
        if (circle != null) // if circle is assigned disable it
            circle.SetActive (false);

        //startFrame = Time.frameCount; // sets current frame when object is spawned
    }

    //void OnEnable()
    //{
    //    m_CacTime = 10.0f;
    //    m_CurIdx = 0;
    //}

    void Update () {

        m_CacTime = m_CacTime + Time.deltaTime;
        //if (0.105f < m_CacTime)
        if (0.10f < m_CacTime)
        {
            if (0 <= m_CacTime && m_CacTime <= (frames.Length - 1))
            {
                for (m_Idx = 0; m_Idx < frames.Length; m_Idx++)
                { 
                    if (m_CurIdx == m_Idx)
                        frames[m_Idx].SetActive(true);
                    else
                        frames[m_Idx].SetActive(false);
                }
            }

            m_CurIdx = m_CurIdx + 1;
            if (frames.Length + 1 < m_CurIdx)
                m_CurIdx = 0;

            m_CacTime = 0.0f;
        }



        //// pacing is current Time.FrameCount updated to reflect our usage
        //// set Time.frameCount
        //// minus itself to zero it out at start when prefab is spawned into the scene
        //// minus timeLoop, that will also zero it out generating a loop
        //// minus frameOffset in order to make a offset basically functions like a delay before starting the animation
        //pacing = (int)(((Time.frameCount - startFrame - timeLoop) / frameSpeed) - frameOffset);

        //// resets the animation loop
        //if (loop == true && pacing > frames.Length && timeLoop != Time.frameCount)
        //{
        //    timeLoop = Time.frameCount;
        //}
        //else
        //{
        //    if (pacing > frames.Length + 2)
        //    { // self destroy gameobject if the loop is not activated
        //        Destroy(gameObject);
        //    }
        //}

        //if (pacing <= frames.Length - 1 && pacing >= 0) // offset frame number with minus 1 in order to convert from ex: 1-6 frame to 0-5 index
        //{
        //    currentFrame = (int)pacing; // float to int convert
        //    frames[currentFrame].SetActive(true); // activate the current active gameobject
        //}
        //if (pacing <= frames.Length && pacing >= 1)
        //{ // disable all previous gameobject that was active in the scene
        //    previousFrame = (int)pacing - 1;
        //    frames[previousFrame].SetActive(false);
        //}
        //if (circle != null)
        //{ // check if there's a gameobject called circle preseted
        //    if (currentFrame >= 0 && currentFrame <= 2)
        //    { // between frame and frame to have it active
        //        circle.SetActive(true);
        //    }
        //    else
        //    {
        //        circle.SetActive(false);
        //    }
        //}

        //if (m_CurIdx != (int)pacing)
        //{
        //    Debug.Log(m_CurIdx + " : " + m_CacTime);
        //    m_CurIdx = (int)pacing;
        //    m_CacTime = 0.0f;
        //}

    }
}