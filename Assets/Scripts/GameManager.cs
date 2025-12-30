using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public NetworkVariable<int> redScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> blueScore = new NetworkVariable<int>(0);

    // ARTIK ÝKÝ AYRI KUTU VAR
    public TMP_Text redScoreText;  // Kýrmýzý takýmýn skoru (Örn: Saðdaki yazý)
    public TMP_Text blueScoreText; // Mavi takýmýn skoru (Örn: Soldaki yazý)

    public GameObject ballPrefab; // Topun Prefab'i
    private GameObject currentBall; // Sahnedeki top

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        // Skor deðiþince UI güncelle
        redScore.OnValueChanged += (oldVal, newVal) => UpdateScoreUI();
        blueScore.OnValueChanged += (oldVal, newVal) => UpdateScoreUI();
        UpdateScoreUI();

        if (IsServer)
        {
            FindBall();
        }
    }

    private void FindBall()
    {
        GameObject ball = GameObject.FindGameObjectWithTag("Ball");
        if (ball != null) currentBall = ball;
    }

    private void UpdateScoreUI()
    {
        // Mavi skoru güncelle
        if (blueScoreText != null)
        {
            blueScoreText.text = "MAVÝ: " + blueScore.Value.ToString();
        }

        // Kýrmýzý skoru güncelle
        if (redScoreText != null)
        {
            redScoreText.text = "KIRMIZI: " + redScore.Value.ToString();
        }
    }

    public void GoalScored(string scoringTeam)
    {
        if (!IsServer) return;

        if (scoringTeam == "Blue") blueScore.Value++;
        else redScore.Value++;

        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        yield return new WaitForSeconds(1f); // 1 saniye bekle

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerScript = client.PlayerObject.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                playerScript.ResetPosition();
            }
        }

        ResetBall();
    }

    private void ResetBall()
    {
        if (currentBall == null) FindBall();

        if (currentBall != null)
        {
            Rigidbody ballRb = currentBall.GetComponent<Rigidbody>();
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
            currentBall.transform.position = new Vector3(0, 5f, 0f);
        }
        else
        {
            GameObject newBall = Instantiate(ballPrefab, new Vector3(0, 5f, 0f), Quaternion.identity);
            newBall.GetComponent<NetworkObject>().Spawn();
            currentBall = newBall;
        }
    }
}