using UnityEngine;
using Unity.Netcode;
using TMPro;

public class TeamUIManager : MonoBehaviour
{
    public GameObject selectionPanel;
    public TMP_Text joinCodeText;

    private void Update()
    {
        if (joinCodeText != null)
        {
            if (!string.IsNullOrEmpty(RelayManager.CurrentJoinCode) && RelayManager.CurrentJoinCode != "---")
            {
                joinCodeText.text = "Oda Kodu: " + RelayManager.CurrentJoinCode;
            }
        }
    }

    public void SelectRedTeam()
    {
        SelectTeam(true); // Kýrmýzý
    }

    public void SelectBlueTeam()
    {
        // DÜZELTÝLDÝ: Burasý eskiden true idi, þimdi false oldu.
        SelectTeam(false); // Mavi
    }

    private void SelectTeam(bool isRed)
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            localPlayer.GetComponent<PlayerController>().SetTeamServerRpc(isRed);

            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }
        }
        else
        {
            Debug.LogError("Oyuncu objesi bulunamadý! Baðlantý kopmuþ olabilir.");
        }
    }
}