using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Oyun Kurallarý")]
    public int maxScore = 5;

    [Header("Skor ve UI")]
    public NetworkVariable<int> redScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> blueScore = new NetworkVariable<int>(0);
    public TMP_Text redScoreText;
    public TMP_Text blueScoreText;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TMP_Text winnerText;
    public GameObject restartButton;

    [Header("Süre")]
    public TMP_Text timerText;
    public NetworkVariable<float> gameTimer = new NetworkVariable<float>(120f);
    private bool isTimerRunning = false; // Baþlangýçta KAPALI (Süre akmasýn)

    [Header("Ses Sistemi")]
    public AudioSource audioSource;
    public AudioClip kickSound;
    public AudioClip whistleSound;
    public AudioClip crowdSound1;
    public AudioClip crowdSound2;

    [Header("Efektler")]
    public ParticleSystem redConfetti;
    public ParticleSystem blueConfetti;

    [Header("Top Ayarlarý")]
    public GameObject ballPrefab;
    public Transform ballSpawnPoint;
    private GameObject currentBall;

    public bool isGameActive = false; // Baþlangýçta KAPALI (Hareket olmasýn)
    private bool hasMatchStarted = false; // Maç baþladý mý kontrolü

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        redScore.OnValueChanged += (oldVal, newVal) => UpdateScoreUI();
        blueScore.OnValueChanged += (oldVal, newVal) => UpdateScoreUI();
        UpdateScoreUI();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (IsServer)
        {
            ResetBall();
            gameTimer.Value = 120f;
            // BURADA ARTIK DÜDÜK KOMUTU YOK.
        }
    }

    private void Update()
    {
        // Sadece Sunucu oyunu yönetir
        if (!IsServer)
        {
            UpdateTimerUI();
            return;
        }

        // --- YENÝ BAÞLANGIÇ MANTIÐI ---
        // Oyun henüz baþlamadýysa ve içeride EN AZ 1 oyuncu varsa sayacý baþlat
        if (!hasMatchStarted && NetworkManager.Singleton.ConnectedClientsList.Count > 0)
        {
            StartCoroutine(StartMatchRoutine());
        }
        // ------------------------------

        if (isGameActive && isTimerRunning)
        {
            if (gameTimer.Value > 0) gameTimer.Value -= Time.deltaTime;
            else EndGameByTime();
        }

        UpdateTimerUI();
    }

    // --- MAÇI GECÝKMELÝ BAÞLATAN FONKSÝYON ---
    private IEnumerator StartMatchRoutine()
    {
        hasMatchStarted = true; // Tekrar tekrar girmeyi engelle

        Debug.Log("Oyuncular geldi. Maç 3 saniye içinde baþlýyor...");

        // 3 Saniye bekle (Oyuncular takým seçsin diye)
        yield return new WaitForSeconds(3f);

        // Þimdi Düdüðü Çal
        PlayWhistleSoundClientRpc();

        // Oyunu Aktif Et (Artýk hareket edebilirler)
        isGameActive = true;
        isTimerRunning = true;
    }

    private void UpdateScoreUI()
    {
        if (blueScoreText != null) blueScoreText.text = "Blue: " + blueScore.Value.ToString();
        if (redScoreText != null) redScoreText.text = "Red: " + redScore.Value.ToString();
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            float time = gameTimer.Value;
            timerText.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(time / 60), Mathf.FloorToInt(time % 60));
            timerText.color = time <= 10f ? Color.red : Color.white;
        }
    }

    // --- RPC FONKSÝYONLARI ---
    [ClientRpc]
    public void PlayKickSoundClientRpc() { if (audioSource && kickSound) audioSource.PlayOneShot(kickSound); }

    [ClientRpc]
    public void PlayWhistleSoundClientRpc() { if (audioSource && whistleSound) audioSource.PlayOneShot(whistleSound); }

    [ClientRpc]
    public void PlayGoalSoundClientRpc(int i)
    {
        if (!audioSource) return;
        if (i == 0 && crowdSound1) audioSource.PlayOneShot(crowdSound1);
        else if (i == 1 && crowdSound2) audioSource.PlayOneShot(crowdSound2);
    }

    [ClientRpc]
    public void PlayConfettiClientRpc(bool playRedConfetti)
    {
        if (playRedConfetti)
        {
            if (redConfetti != null) redConfetti.Play();
        }
        else
        {
            if (blueConfetti != null) blueConfetti.Play();
        }
    }

    // --- GOL VE EFEKT MANTIÐI (DÜZELTÝLMÝÞ) ---
    public void GoalScored(string scoringTeam)
    {
        if (!IsServer || !isGameActive) return;

        // Gol sesini çal
        PlayGoalSoundClientRpc(Random.Range(0, 2));

        if (scoringTeam.Trim() == "Blue")
        {
            blueScore.Value++;
            // Mavi Skor Aldý -> TERSÝNÝ ÝSTEDÝN -> KIRMIZI Konfeti (True)
            PlayConfettiClientRpc(true);
        }
        else if (scoringTeam.Trim() == "Red")
        {
            redScore.Value++;
            // Kýrmýzý Skor Aldý -> TERSÝNÝ ÝSTEDÝN -> MAVÝ Konfeti (False)
            PlayConfettiClientRpc(false);
        }

        if (redScore.Value >= maxScore) EndGame("KIRMIZI TAKIM KAZANDI!");
        else if (blueScore.Value >= maxScore) EndGame("MAVÝ TAKIM KAZANDI!");
        else HandleRoundReset();
    }

    private void EndGameByTime()
    {
        if (redScore.Value > blueScore.Value) EndGame("SÜRE BÝTTÝ! KIRMIZI KAZANDI!");
        else if (blueScore.Value > redScore.Value) EndGame("SÜRE BÝTTÝ! MAVÝ KAZANDI!");
        else EndGame("SÜRE BÝTTÝ! BERABERE!");
    }

    private void EndGame(string message)
    {
        isGameActive = false;
        GameOverClientRpc(message);
    }

    [ClientRpc]
    private void GameOverClientRpc(string message)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (winnerText != null) winnerText.text = message;
            if (restartButton != null) restartButton.SetActive(IsServer);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void RestartGame()
    {
        if (IsServer) NetworkManager.Singleton.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    private void HandleRoundReset()
    {
        isGameActive = false;
        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        yield return new WaitForSeconds(2f);

        if (redScore.Value < maxScore && blueScore.Value < maxScore)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var playerScript = client.PlayerObject.GetComponent<PlayerController>();
                    if (playerScript != null) playerScript.ResetPosition();
                }
            }

            ResetBall();
            gameTimer.Value = 120f;

            // Gol sonrasý tekrar baþlama düdüðü
            PlayWhistleSoundClientRpc();

            isGameActive = true;
        }
    }

    private void ResetBall()
    {
        Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position : new Vector3(0, 5f, 0f);
        if (currentBall == null) currentBall = GameObject.FindGameObjectWithTag("Ball");

        if (currentBall != null)
        {
            Rigidbody ballRb = currentBall.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                ballRb.isKinematic = true;
                currentBall.transform.position = spawnPos;
                ballRb.isKinematic = false;
            }
        }
        else if (ballPrefab != null)
        {
            GameObject newBall = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
            newBall.GetComponent<NetworkObject>().Spawn();
            currentBall = newBall;
        }
    }
}