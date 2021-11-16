using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public enum GameDifficulty
{
    Stage_1 = 0,
    Stage_2,
    Stage_3,
    Stage_4,
    Stage_5,
}

public enum UserType
{
    SapphiArtchan = 0,
    SK_Bei_T_pose = 1,
}

public class GlobalValue
{
    //게임 난이도
    public static GameDifficulty g_GameDifficulty = GameDifficulty.Stage_1;

    //유저의 고유번호 및 닉네임 및 캐릭터
    public static string   g_Unique_ID = "";                    //유저의 고유번호
    public static string   g_NickName = "";                     //유저의 별명
    public static UserType g_UserType = UserType.SapphiArtchan; //유저의 캐릭터 종류

    //유저의 경험치 및 골드값
    public static float  g_UserExp = 0.0f;         //유저의 경험치
    public static int    g_UserGold = 0;           //게임머니

    //유저의 스텟
    public static float  g_UserAttackRate = 0.0f; //유저의 공격력
    public static float  g_UserHP = 0.0f;        //유저의 HP
    public static float  g_UserDef = 0.0f;
    public static float  g_SkillAttackRate = 0.0f;

    public static void newCreateUser(UserType _newType)
    {
        g_UserGold = 0;
        g_UserExp = 0.0f;
        g_UserType = _newType;
        g_UserAttackRate = 10.0f;
        g_SkillAttackRate = g_UserAttackRate * 5.0f;
        g_UserHP = 1000.0f;
        g_UserDef = 0.0f;
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>()
            {
                { "UserGold",g_UserGold.ToString() },
                { "UserCharType",((int)g_UserType).ToString() },
                { "UserExp",g_UserExp.ToString() },
                { "UserAttackRate",g_UserAttackRate.ToString() },
                { "UserDef",g_UserDef.ToString() },
                { "UserHP",g_UserHP.ToString() },
            }
        };

        PlayFabClientAPI.UpdateUserData(
                request,
                (result) =>
                {
                    //업데이트 성공 처리
                    Debug.Log("유저 데이터 저장 성공");
                },

                (error) =>
                {
                    //업데이트 실패시 응답 함수
                    Debug.Log("유저 데이터 저장 실패");
                }
            );
    }

} 