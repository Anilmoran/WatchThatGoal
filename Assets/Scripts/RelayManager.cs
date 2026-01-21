using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using TMPro;

public class RelayManager : MonoBehaviour
{
    public static string CurrentJoinCode { get; private set; }
    private const int MaxPlayers = 4;

    [Header("UI Ayarlarý")]
    public TMP_InputField joinCodeInputField;

    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Servis hatasý: " + e.Message);
        }
    }

    public async void CreateRelayButton()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient) return;
        await CreateRelay();
    }

    public async Task<string> CreateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            CurrentJoinCode = joinCode;

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // Önce Host'u baþlatýyoruz
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Host Baþlatýldý. Kod: " + joinCode);

                // Sahne yüklemesini bir kare sonra yapmasý için garantiye alýyoruz
                // Sahne isminin "matchgame" olduðundan ve Build Settings'te olduðundan emin ol
                NetworkManager.Singleton.SceneManager.LoadScene("matchgame", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }

            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay hatasý: " + e.Message);
            return null;
        }
    }

    public async void JoinRelay(string joinCode)
    {
        joinCode = joinCode?.Trim();
        if (string.IsNullOrEmpty(joinCode) || NetworkManager.Singleton.IsClient) return;

        try
        {
            CurrentJoinCode = joinCode;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
            Debug.Log("Client baþlatýldý, sahne senkronizasyonu bekleniyor...");
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Katýlma hatasý: " + e.Message);
        }
    }

    public void JoinRelayButton()
    {
        if (joinCodeInputField != null) JoinRelay(joinCodeInputField.text);
    }
}