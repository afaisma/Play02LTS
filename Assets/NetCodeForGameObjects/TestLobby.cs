using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QFSW.QC;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Random = UnityEngine.Random;

public class TestLobby : MonoBehaviour
{
    private Lobby hostLobby;
    private float heartBeatTimer;
    private string playerId;
    private async void Start()
    {
        await UnityServices.InitializeAsync();
        AuthenticationService.Instance.SignedIn += () =>
        {
            playerId = AuthenticationService.Instance.PlayerId;
            Debug.Log("Signed in S" + playerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void HandlehearbeatTimer()
    {
        if (hostLobby != null)
        {
            heartBeatTimer -= Time.deltaTime;
            if (heartBeatTimer <= 0f)
            {
                float heartBeatTimerMax = 15;
                heartBeatTimer = heartBeatTimerMax;
                LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
        }
    }
    
    private async void Update()
    {
        HandlehearbeatTimer();
    }
    
    [Command]
    private async void CreateLobby()
    {
        try
        {
            string lobbyName = "MyLobby";
            int maxPlayers = 4;
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers);
            hostLobby = lobby;
            Debug.Log("Lobby created: " + lobbyName + ", " + lobby.Id); ;
        }
        catch (System.Exception e)
        {
            Debug.Log("Error creating lobby: " + e.Message);
        }
    }
    
    [Command]
    private async void ListLobbies()
    {
        QueryLobbiesOptions options = new QueryLobbiesOptions();
        options.Count = 25;
        options.Filters = new List<QueryFilter> {
            new QueryFilter(
                field: QueryFilter.FieldOptions.AvailableSlots,
                op: QueryFilter.OpOptions.GT,
                value: "0")
        };
        options.Order = new List<QueryOrder> {
            new QueryOrder(
                asc: false,
                field: QueryOrder.FieldOptions.Created)
        };
        QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);
        Debug.Log("Lobbies found: " + queryResponse.Results.Count);
        foreach (var lobby in queryResponse.Results)
        {
            Debug.Log("Lobby: " + lobby.Name + ", " + lobby.Id);
        }
    }

}
