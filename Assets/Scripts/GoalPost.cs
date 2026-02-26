using UnityEngine;
using Unity.Netcode;

public class GoalPost : NetworkBehaviour
{
    // Inspector'a not düþtük: Sadece "Red" veya "Blue" yazýlmalý.
    [Tooltip("Buraya sadece 'Red' veya 'Blue' yazýn.")]
    public string teamWhoScores;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Ball"))
        {
            // Eðer boþ bir þey yazýldýysa uyarý ver
            if (string.IsNullOrEmpty(teamWhoScores))
            {
                Debug.LogError("HATA: Bu kalenin GoalPost scriptinde takým ismi boþ býrakýlmýþ!");
                return;
            }

            Debug.Log($"Top kaleye girdi! Puaný alacak takým: {teamWhoScores}");
            GameManager.Instance.GoalScored(teamWhoScores);
        }
    }
}