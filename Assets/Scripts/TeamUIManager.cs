using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

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
        StartCoroutine(EnsurePlayerAndSelect(true));
    }

    public void SelectBlueTeam()
    {
        StartCoroutine(EnsurePlayerAndSelect(false));
    }

    private IEnumerator EnsurePlayerAndSelect(bool isRed)
    {
        // Oyuncu objesi spawn olana kadar bekle
        float timeout = 2f;
        while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
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
            Debug.LogError("Oyuncu objesi bulunamadý! Sahne yüklenememiþ olabilir.");
        }
    }
}