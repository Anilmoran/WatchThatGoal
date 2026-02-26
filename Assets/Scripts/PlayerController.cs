using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class PlayerController : NetworkBehaviour
{
    [Header("Animasyon")]
    private Animator anim;

    // --- YENİ EKLENEN: AĞ ÜZERİNDEN SENKRONİZE KOŞMA DEĞİŞKENİ ---
    // Bu değişkeni sahibi değiştirebilir, herkes okuyabilir!
    public NetworkVariable<bool> isRunning = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Hareket Ayarları")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Şut Ayarları")]
    [SerializeField] private float minKickForce = 10f;
    [SerializeField] private float maxKickForce = 25f;
    [SerializeField] private float maxChargeTime = 1.0f;
    [SerializeField] private float passivePushForce = 5f;
    [SerializeField] private float kickLift = 0.15f;

    [Header("UI Referansları")]
    [SerializeField] private Slider powerBarSlider;

    [Header("Kamera & Mouse")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -85f;
    [SerializeField] private float maxVerticalAngle = 85f;

    [Header("Görsel Ayarlar")]
    [SerializeField] private SkinnedMeshRenderer characterMesh;

    [Header("Kamera Takip")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2f, -6f);
    [SerializeField] private float cameraSmoothness = 20f;

    public NetworkVariable<Color> playerColor = new NetworkVariable<Color>(Color.gray);
    public NetworkVariable<bool> isRedTeam = new NetworkVariable<bool>(false);

    private Rigidbody rb;
    private bool canMove = false;
    private Camera mainCamera;
    private bool isGrounded = true;

    private float currentRotationY = 0f;
    private float currentVerticalRotation = 0f;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private float currentChargeTime = 0f;
    private bool isCharging = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
        anim = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        playerColor.OnValueChanged += OnColorChanged;
        if (characterMesh != null) characterMesh.material.color = playerColor.Value;

        canMove = false;

        if (IsOwner)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (powerBarSlider != null)
            {
                powerBarSlider.gameObject.SetActive(false);
                powerBarSlider.value = 0;
            }
        }
        else
        {
            if (powerBarSlider != null) powerBarSlider.gameObject.SetActive(false);
        }
    }

    private void OnColorChanged(Color oldColor, Color newColor)
    {
        if (characterMesh != null) characterMesh.material.color = newColor;
    }

    private void Update()
    {
        // --- HERKES İÇİN ÇALIŞAN KISIM (ANİMASYON SENKRONİZASYONU) ---
        // Sahibi olsak da olmasak da, bu karakterin isRunning değeri neyse o animasyonu oynatıyoruz.
        if (anim != null)
        {
            anim.SetBool("IsRunning", isRunning.Value);
        }

        if (GameManager.Instance != null && !GameManager.Instance.isGameActive)
        {
            return;
        }

        // --- SADECE SAHİBİ İÇİN ÇALIŞAN KISIM ---
        if (!IsOwner) return;

        if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return; }
        if (!canMove) return;

        HandleInput();
        HandleCamera(false);
        HandleKickCharge();
        HandleGoalkeeperAction();

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            // Sadece kendimizde değil, herkeste zıplama animasyonunu oynat!
            PlayJumpAnimServerRpc();
        }
    }

    // --- ANİMASYON RPC'LERİ (AĞ ÜZERİNDEN TETİKLEMELER) ---
    [ServerRpc] private void PlayJumpAnimServerRpc() { PlayJumpAnimClientRpc(); }
    [ClientRpc] private void PlayJumpAnimClientRpc() { if (anim != null) anim.SetTrigger("Jump"); }

    [ServerRpc] private void PlayKickAnimServerRpc() { PlayKickAnimClientRpc(); }
    [ClientRpc] private void PlayKickAnimClientRpc() { if (anim != null) anim.SetTrigger("Kick"); }

    [ServerRpc] private void PlayDiveAnimServerRpc() { PlayDiveAnimClientRpc(); }
    [ClientRpc] private void PlayDiveAnimClientRpc() { if (anim != null) anim.SetTrigger("Dive"); }
    // --------------------------------------------------------

    private void HandleGoalkeeperAction()
    {
        if (Input.GetMouseButtonDown(1))
        {
            bool canDive = false;
            float limitX = 8f;

            if (isRedTeam.Value && transform.position.x < -limitX) canDive = true;
            else if (!isRedTeam.Value && transform.position.x > limitX) canDive = true;

            if (canDive)
            {
                PlayDiveAnimServerRpc(); // Herkeste dalış animasyonu
            }
        }
    }

    private void HandleKickCharge()
    {
        if (Input.GetMouseButton(0))
        {
            isCharging = true;
            currentChargeTime += Time.deltaTime;
            if (currentChargeTime > maxChargeTime) currentChargeTime = maxChargeTime;

            if (powerBarSlider != null)
            {
                powerBarSlider.gameObject.SetActive(true);
                powerBarSlider.value = currentChargeTime / maxChargeTime;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isCharging)
            {
                float chargeRatio = currentChargeTime / maxChargeTime;
                float finalKickForce = Mathf.Lerp(minKickForce, maxKickForce, chargeRatio);

                TryKick(finalKickForce);

                PlayKickAnimServerRpc(); // Herkeste şut animasyonu

                isCharging = false;
                currentChargeTime = 0f;
                if (powerBarSlider != null) powerBarSlider.gameObject.SetActive(false);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !canMove) return;
        if (GameManager.Instance != null && !GameManager.Instance.isGameActive) return;

        ApplyMovementAndRotation();
        CheckGround();
    }

    private void HandleInput()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        currentRotationY += mouseX;

        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        currentVerticalRotation -= mouseY;
        currentVerticalRotation = Mathf.Clamp(currentVerticalRotation, minVerticalAngle, maxVerticalAngle);
    }

    private void ApplyMovementAndRotation()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Quaternion cameraYaw = Quaternion.Euler(0f, currentRotationY, 0f);
        Vector3 forward = cameraYaw * Vector3.forward;
        Vector3 right = cameraYaw * Vector3.right;

        Vector3 moveDir = (forward * v) + (right * h);
        if (moveDir.magnitude > 1f) moveDir.Normalize();

        if (moveDir.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 15f));
        }
        else
        {
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, cameraYaw, Time.fixedDeltaTime * 15f));
        }

        Vector3 targetVelocity = moveDir * speed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

        // --- DEĞİŞTİRİLDİ: Koşma durumunu ağ değişkenine (NetworkVariable) yaz ---
        bool moving = moveDir.magnitude > 0.1f;
        if (isRunning.Value != moving)
        {
            isRunning.Value = moving;
        }
    }

    private void HandleCamera(bool snapToPosition)
    {
        if (mainCamera == null) return;
        Quaternion cameraRotation = Quaternion.Euler(currentVerticalRotation, currentRotationY, 0);
        Vector3 targetPosition = transform.position + Vector3.up * 1.5f + (cameraRotation * cameraOffset);
        if (snapToPosition) mainCamera.transform.position = targetPosition;
        else mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, cameraSmoothness * Time.deltaTime);
        mainCamera.transform.LookAt(transform.position + Vector3.up * 1.5f);
    }

    private void CheckGround()
    {
        isGrounded = false;

        Vector3 origin = transform.position + (Vector3.up * 1.0f);
        float maxRayDistance = 4.0f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxRayDistance);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Ground"))
            {
                isGrounded = true;
                break;
            }
        }
    }

    private void TryKick(float force)
    {
        Vector3 flatForward = mainCamera.transform.forward;
        flatForward.y = 0; flatForward.Normalize();

        Collider[] hits = Physics.OverlapSphere(transform.position + flatForward * 1.5f, 2.5f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Ball"))
            {
                var ballNetObj = hit.GetComponent<NetworkObject>();
                if (ballNetObj != null)
                {
                    if (GameManager.Instance != null) GameManager.Instance.PlayKickSoundClientRpc();

                    Vector3 kickDir = flatForward + (Vector3.up * kickLift);
                    kickDir.Normalize();

                    KickBallServerRpc(ballNetObj.NetworkObjectId, kickDir, force);
                    break;
                }
            }
        }
    }

    [ServerRpc]
    private void KickBallServerRpc(ulong ballNetworkId, Vector3 direction, float force)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ballNetworkId, out NetworkObject ballNetObj))
        {
            Rigidbody ballRb = ballNetObj.GetComponent<Rigidbody>();
            if (ballRb != null) ballRb.AddForce(direction * force, ForceMode.Impulse);
        }
    }

    [ServerRpc]
    public void SetTeamServerRpc(bool redTeam)
    {
        isRedTeam.Value = redTeam;

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;
        float startRotY = 0f;
        GameObject spawnObj = null;

        if (redTeam)
        {
            playerColor.Value = Color.red;
            spawnObj = GameObject.Find("SpawnPoint_Red");
            startRotY = 90f;
        }
        else
        {
            playerColor.Value = Color.blue;
            spawnObj = GameObject.Find("SpawnPoint_Blue");
            startRotY = -90f;
        }

        if (spawnObj != null)
        {
            spawnPos = spawnObj.transform.position;
            spawnRot = spawnObj.transform.rotation;
        }
        else
        {
            if (redTeam) spawnPos = new Vector3(-25f, 5f, 0f);
            else spawnPos = new Vector3(25f, 5f, 0f);
            spawnRot = Quaternion.Euler(0, startRotY, 0);
        }

        initialPosition = spawnPos;
        initialRotation = spawnRot;
        currentRotationY = startRotY;
        TeleportPlayerClientRpc(spawnPos, spawnRot, startRotY);
    }

    public void ResetPosition()
    {
        if (IsServer)
        {
            float rotY = initialRotation.eulerAngles.y;
            TeleportPlayerClientRpc(initialPosition, initialRotation, rotY);
        }
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 pos, Quaternion rot, float newRotationY)
    {
        if (anim != null) anim.SetTrigger("StandUp");

        if (IsOwner)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;

            transform.position = pos;
            transform.rotation = rot;
            currentRotationY = newRotationY;
            currentVerticalRotation = 0f;

            rb.isKinematic = wasKinematic;
            canMove = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (mainCamera != null)
            {
                Quaternion cameraRotation = Quaternion.Euler(currentVerticalRotation, currentRotationY, 0);
                Vector3 targetPosition = transform.position + Vector3.up * 1.5f + (cameraRotation * cameraOffset);
                mainCamera.transform.position = targetPosition;
                mainCamera.transform.LookAt(transform.position + Vector3.up * 1.5f);
            }

            isCharging = false;
            currentChargeTime = 0f;
            if (powerBarSlider != null) powerBarSlider.gameObject.SetActive(false);
        }
    }

    // --- TOP FİZİĞİ ÇÖZÜMÜ ---
    private void OnCollisionStay(Collision collision)
    {
        // TOPU FİZİKSEL OLARAK İTME YETKİSİ SADECE SERVER'DADIR.
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ball"))
        {
            Rigidbody ballRb = collision.gameObject.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                // Çarpan kişi herhangi bir oyuncu olabilir (Player 1 veya Player 2). 
                // Eğer o oyuncu "koşuyor" ise Server topu o yöne doğru itecek.
                if (isRunning.Value)
                {
                    // Oyuncudan topa doğru olan yönü buluyoruz
                    Vector3 pushDirection = collision.transform.position - transform.position;
                    pushDirection.y = 0;
                    pushDirection.Normalize();

                    ballRb.AddForce(pushDirection * passivePushForce, ForceMode.Acceleration);
                }
            }
        }
    }
}