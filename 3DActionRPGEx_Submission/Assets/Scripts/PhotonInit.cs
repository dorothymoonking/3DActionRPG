﻿using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PhotonInit : MonoBehaviourPunCallbacks
{
    //플레이어 이름을 입력하는 UI 항목 연결
    public InputField userId;
    public Button JoinRandomRoomBtn;

    //룸 이름을 입력받을 UI 항목 연결 변수
    public InputField roomName;
    public Button CreateRoomBtn;
    public Button ExitMatchBtn;

    //--------룸 목록 갱신을 위한 변수들
    //RoomItemdl 차일드로 생성될 Parent 객체
    public GameObject scrollContents;
    //룸 목록만큼 생성될 RoomItem 프리팹
    public GameObject roomItem;
    List<RoomInfo> myList = new List<RoomInfo>();
    //--------룸 목록 갱신을 위한 변수들

    //----- 룸 생성시 난이도 설정을 위한 변수들
    public GameObject StageSettingPanel = null;
    public Toggle[] StageToggle = null;
    public Button StageSetDoneBtn = null;
    public Button StageSetDoneClose = null;
    int StageNum = 0;
    //-----

    static public bool isFocus = true;

    private void OnApplicationFocus(bool focus)  //윈도우 창 활성화 비활성화 일때
    {
        PhotonInit.isFocus = focus;
    }

    void Awake()
    {
        //포톤 클라우드 서버 접속 여부 확인(인게임에서 빠져나 온 경우가 있기 때문에...)
        if (!PhotonNetwork.IsConnected)
        {
            //1번, 포톤 클라우드에 접속
            PhotonNetwork.ConnectUsingSettings();
            //포톤 서버에 접속시도(지역 서버 접속) -> 사용자 인증 -> 로비 입장 진행
        }

        //사용자 이름 설정
        userId.text = GetUserId();

        //룸 이름을 무작위로 설정
        roomName.text = "Room_" + Random.Range(0, 999).ToString("000");

    } //void Awake()

    // Start is called before the first frame update
    void Start()
    {
        if (JoinRandomRoomBtn != null)
            JoinRandomRoomBtn.onClick.AddListener(ClickJoinRandomRoom);

        if (CreateRoomBtn != null)
            CreateRoomBtn.onClick.AddListener(StageSetting);

        if(StageSetDoneBtn != null)
            StageSetDoneBtn.onClick.AddListener(ClickCreateRoom);

        if (StageSetDoneClose != null)
            StageSetDoneClose.onClick.AddListener(() => 
            {
                if(StageSettingPanel != null && StageSettingPanel.activeSelf == true)
                    StageSettingPanel.SetActive(false);
            });

        //매치 매이킹 로비를 빠져나가기...
        if (ExitMatchBtn != null)
        {
            ExitMatchBtn.onClick.AddListener(() => {
                PhotonNetwork.Disconnect(); //포톤 클라우드 서버 접속 연결 끊기
                UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
            });
        }//if (ExitMatchingBtn != null)
    }

    //// Update is called once per frame
    //void Update()
    //{
    //}

    //2번, ConnectUsingSettings() 함수 호출에 대한 서버 접속이 성공하면 호출되는 콜백 함수
    //PhotonNetwork.LeaveRoom(); 으로 방을 떠날 때도 이 함수가 자동으로 호출된다.
    public override void OnConnectedToMaster()
    {   //여기서 Master는 포톤의 지역 서버를 의미한다.
        Debug.Log("서버 접속 완료");
        //단순 포톤 서버 접속만 된 상태 (ConnectedToMaster)

        //3번, 규모가 작은 게임에서는 로비가 보통 하나이고...
        PhotonNetwork.JoinLobby();
        //대형 게임인 경우 상급자로비, 중급자로비, 초보자로비 처럼 
        //로비가 여러개일 수 있다. 
    }

    //4번, PhotonNetwork.JoinLobby() 성공시 호출되는 로비 접속 콜백함수
    public override void OnJoinedLobby()
    {
        Debug.Log("로비접속완료");
        userId.text = GetUserId(); //방에서 로비로 나올 때도 유저 ID를 하나 셋팅해 주어야 한다.

        //무작위로 추출된 방으로 입장
        //ExitGames.Client.Photon.Hashtable roomProperties =
        //new ExitGames.Client.Photon.Hashtable() { { "map", 1 }, { "minLevel", 10 } };
        //PhotonNetwork.JoinRandomRoom(roomProperties, 4);
        //PhotonNetwork.JoinRandomRoom();
    }

    //PhotonNetwork.CreateRoom() 이 함수가 성공하면 2번째로 자동으로 호출되는 함수
    //PhotonNetwork.JoinRoom() 함수가 성공해도 자동으로 호출되는 함수
    //PhotonNetwork.JoinRandomRoom(); 함수가 성공해도 자동으로 호출되는 함수
    public override void OnJoinedRoom()
    {//서버역할인 경우 6번 : 방입장, 클라이언트 역할인 경우 5번 : 방입장
        Debug.Log("방 참가 완료");

        //CreateTank();  //<---- 테스트 코드
        //룸 씬으로 이동하는 코루틴 실행
        StartCoroutine(this.LoadBattleField());
    }

    //룸 씬으로 이동하는 코루틴 함수
    IEnumerator LoadBattleField()        //최종 배틀필드 씬 로딩 --> 6번 or 5번
    {
        //씬을 이동하는 동안 포톤 클라우드 서버로부터 네트워크 메시지 수신 중단
        PhotonNetwork.IsMessageQueueRunning = false;
        //백그라운드로 씬 로딩

        Time.timeScale = 1.0f;  //게임에 들어갈 때는 원래 속도로...

        AsyncOperation ao =
          UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("InGame");

        yield return ao;
    }

    public void StageSetting()
    {
        if (StageSettingPanel != null && StageSettingPanel.activeSelf == false)
            StageSettingPanel.SetActive(true);
    }

    public void ClickJoinRandomRoom()         //3번 방 입장 요청 버튼 누름
    {
        //UnityEngine.SceneManagement.SceneManager.LoadScene("InGame");

        //로컬 플레이어의 이름을 설정
        PhotonNetwork.LocalPlayer.NickName = userId.text;
        //플레이어 이름을 저장
        PlayerPrefs.SetString("USER_ID", userId.text);

        //5번 무작위로 추출된 방으로 입장
        PhotonNetwork.JoinRandomRoom();
    }

    //PhotonNetwork.JoinRandomRoom() 이 함수 실패한 경우 호출되는 콜백 함수
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("랜덤 방 참가 실패 (참가할 방이 존재하지 않습니다.)");

        //룸 생성
        PhotonNetwork.CreateRoom("MyRoom");
        // 방이 없을 때는 내가 방을 만들고 입장해 버린다.
        // ( 5번 랜덤 로그인 시에 서버 역할을 하게 될 Client는 이쪽으로 들어오게 될 것이다.)
    }

    public void ClickCreateRoom()
    {
        for (int i = 0; i < StageToggle.Length; i++)
        {
            if (StageToggle[i].isOn == true)
                GlobalValue.g_GameDifficulty = (GameDifficulty)i;
        }
        StageNum = ((int)GlobalValue.g_GameDifficulty + 1);
        if (StageSettingPanel != null && StageSettingPanel.activeSelf == true)
            StageSettingPanel.SetActive(false);

        string _roomName = roomName.text + "(단계:" + StageNum + ")";
        //룸 이름이 없거나 Null일 경우 룸 이름 지정
        if (string.IsNullOrEmpty(roomName.text))
        {
            _roomName = "ROOM_" + Random.Range(0, 999).ToString("000") + "(단계:" + StageNum + ")";
        }

        //로컬 플레이어의 이름을 설정
        PhotonNetwork.LocalPlayer.NickName = userId.text;
        //플레이어 이름을 저장
        PlayerPrefs.SetString("USER_ID", userId.text);

        //생성할 룸의 조건 설정
        RoomOptions roomOptions = new RoomOptions();  //using Photon.Realtime;
        roomOptions.IsOpen = true;     //입장 가능 여부
        roomOptions.IsVisible = true;  //로비에서 룸의 노출 여부
        roomOptions.MaxPlayers = 8;    //룸에 입장할 수 있는 최대 접속자 수

        //지정한 조건에 맞는 룸 생성 함수
        PhotonNetwork.CreateRoom(_roomName, roomOptions, TypedLobby.Default);
        //TypedLobby.Default 어느 로비에 방을 만들껀지? 
    }

    //(같은 이름의 방이 있을 때 실패함)
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log("방 만들기 실패");
        //주로 같은 이름의 방이 존재할 때 룸생성 에러가 발생된다.
        Debug.Log(returnCode.ToString()); //오류 코드(ErrorCode 클래스)
        Debug.Log(message); //오류 메시지
    }

    //PlayFab에 저장된 함수 적용하는 함수
    string GetUserId()
    {
        string userId = GlobalValue.g_NickName;

        return userId;
    }

    //생성된 룸 목록이 변경됐을 때 호출되는 콜백 함수
    //방 리스트 갱신은 로비에서만 가능하다.
    //   내가 로비로 진입할 때도 OnRoomListUpdate() 함수를 받고
    //   누군가 방을 새로 만들거나 방이 파괴될 때도 OnRoomListUpdate() 함수를 받는다.
    //   A가 로비에서 대기하고 있는데 B가 방을 만들고 들어가면 OnRoomListUpdate()가 
    //로비에서 대기하고 있었던 A쪽에서 호출된다.
    //   B가 방을 만들면서 들어갈 때는 roomList[i].RemoveFromList == false가 되고,
    //   B가 방을 떠나면서 방이 제거되야 할 때 roomList[i].RemoveFromList == true가 될 것이다.
    //   A가 로그아웃(포톤서버에 접속끊기) 했다가 다시 로비까지 들어 올 때도 OnRoomListUpdate() 
    //함수를 받게 된다.
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        int roomCount = roomList.Count;
        for (int i = 0; i < roomCount; i++)
        {
            if (!roomList[i].RemovedFromList)
            {
                if (!myList.Contains(roomList[i])) myList.Add(roomList[i]);
                else myList[myList.IndexOf(roomList[i])] = roomList[i];
            }
            else if (myList.IndexOf(roomList[i]) != -1)
                myList.RemoveAt(myList.IndexOf(roomList[i]));
        }

        //룸 목록을 다시 받았을 때 갱신하기 위해 기존에 생성된 RoomItem을 삭제
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("ROOM_ITEM"))
        {
            Destroy(obj);
        }

        //스크롤 영역 초기화
        scrollContents.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

        for (int i = 0; i < myList.Count; i++)
        {
            //Debug.Log(_room.Name);
            GameObject room = (GameObject)Instantiate(roomItem);
            //생성한 RoomItem 프리팹의 Parent를 지정
            room.transform.SetParent(scrollContents.transform, false);

            //생성한 RoomItem에 표시하기 위한 텍스트 정보 전달
            RoomData roomData = room.GetComponent<RoomData>();
            roomData.roomName = myList[i].Name;
            roomData.connectPlayer = myList[i].PlayerCount;
            roomData.maxPlayer = myList[i].MaxPlayers;

            //텍스트 정보를 표시
            roomData.DispRoomData(myList[i].IsOpen);
            //RoomItem으 Button 컴포넌트에 클릭 이벤트를 동적으로 연결
            //roomData.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(
            //    delegate { OnClickRoomItem(roomData.roomName); });

        }//for (int i = 0; i < roomCount; i++)

    }// public override void OnRoomListUpdate(List<RoomInfo> roomList)

    //RoomItem이 클릭되면 호출될 이벤트 연결 함수
    public void OnClickRoomItem(string roomName)
    {
        //로컬 플레이어의 이름을 설정
        PhotonNetwork.LocalPlayer.NickName = userId.text;
        //플레이어 이름을 저장
        PlayerPrefs.SetString("USER_ID", userId.text);

        //인자로 전달된 이름에 해당하는 룸으로 입장
        PhotonNetwork.JoinRoom(roomName);
    }

#if UNITY_EDITOR && !(UNITY_IPHONE || UNITY_ANDROID)
    void OnGUI()
    {
        //현재 포톤의 상태를 string으로 리턴해 주는 함수
        string a_str = PhotonNetwork.NetworkClientState.ToString();
        GUI.Label(new Rect(10, 1, 1500, 60),
                    "<color=#00ff00><size=35>" + a_str + "</size></color>");
    }
    #endif
}
