using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using TMPro;

public class RelayManager : MonoBehaviour
{
    public static string CurrentJoinCode = "---";
    public TMP_InputField codeInput;

    public void JoinGameByButton()
    {
        string code = codeInput.text;
        if (string.IsNullOrEmpty(code)) { Debug.LogWarning("Kod alaný boþ!"); return; }
        JoinRelay(code.Trim());
    }

    public async void CreateRelay()
    {
        try
        {
            if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            CurrentJoinCode = joinCode;
            Debug.Log("Oda Kodu: " + joinCode);

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene("MatchGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        catch (RelayServiceException e) { Debug.LogError(e); }
    }

    public async void JoinRelay(string joinCode)
    {
        try
        {
            CurrentJoinCode = joinCode;
            if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e) { Debug.LogError(e); }
    }

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}