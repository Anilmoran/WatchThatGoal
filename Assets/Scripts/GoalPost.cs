using UnityEngine;
using Unity.Netcode;

public class GoalPost : NetworkBehaviour
{
    // Bu kaleye gol girerse puaný kim kazanacak?
    // Mavi Kaleye gol girerse -> Red kazanýr.
    // Kýrmýzý Kaleye gol girerse -> Blue kazanýr.
    public string teamWhoScores;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Sadece sunucu golü iþler

        if (other.CompareTag("Ball"))
        {
            // Gol oldu!
            Debug.Log("GOL! Skoru alan: " + teamWhoScores);
            GameManager.Instance.GoalScored(teamWhoScores);
        }
    }
}
