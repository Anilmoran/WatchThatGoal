using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float kickForce = 20f;

    [Header("Mouse Ayarları")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;

    [Header("Görsel Ayarlar")]
    [SerializeField] private MeshRenderer meshRenderer;

    [Header("Kamera Ayarları")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2f, -6f);
    [SerializeField] private float cameraSmoothness = 20f;

    public NetworkVariable<Color> playerColor = new NetworkVariable<Color>(Color.gray);

    private Rigidbody rb;
    private bool canMove = false;
    private Camera mainCamera;
    private bool isGrounded = true;

    private float currentRotationY = 0f;
    private float currentVerticalRotation = 0f;

    // Başlangıç pozisyonlarını burada tutalım ki gol olunca resetleyebilelim
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        playerColor.OnValueChanged += OnColorChanged;
        meshRenderer.material.color = playerColor.Value;
        canMove = false;
    }

    private void OnColorChanged(Color oldColor, Color newColor)
    {
        meshRenderer.material.color = newColor;
    }

    [ServerRpc]

    
    public void SetTeamServerRpc(bool isRedTeam)
    {
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (isRedTeam)
        {
            playerColor.Value = Color.red;
            // Kırmızı Takım: Kalenin önü
            spawnPos = new Vector3(-25f, 2f, 0f);
            spawnRot = Quaternion.Euler(0, 90, 0);
            currentRotationY = 90f;
        }
        else
        {
            playerColor.Value = Color.blue;
            // MAVİ TAKIM DÜZELTME: Kırmızı ile aynı mesafede, diğer kalede spawn olur.
            // Eğer hala çok uzak geliyorsa 25f değerini 15f yaparak kaleye yaklaştırabilirsin.
            spawnPos = new Vector3(25f, 2f, 0f);
            spawnRot = Quaternion.Euler(0, -90, 0);
            currentRotationY = -90f;
        }

        initialPosition = spawnPos;
        initialRotation = spawnRot;

        // Fiziksel yer değiştirme
        TeleportPlayer(spawnPos, spawnRot);

        // Hareket izni ve rotasyon verisini gönder
        EnableMovementClientRpc(currentRotationY);
    }


    // Gol olunca çağrılacak fonksiyon
    public void ResetPosition()
    {
        if (IsServer)
        {
            TeleportPlayer(initialPosition, initialRotation);
        }
    }

    private void TeleportPlayer(Vector3 pos, Quaternion rot)
    {
        // Fiziği sıfırla
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Rigidbody'yi anlık olarak Kinematic yap (Fizik motoru karışmasın)
        bool wasKinematic = rb.isKinematic;
        rb.isKinematic = true;

        // Hem transform hem rigidbody pozisyonunu zorla
        transform.position = pos;
        transform.rotation = rot;
        rb.position = pos;
        rb.rotation = rot;

        // Fiziği geri aç
        rb.isKinematic = wasKinematic;

        // Clientlarda da pozisyonu zorla senkronize et (ClientNetworkTransform kullanıyorsan)
        Physics.SyncTransforms();
    }

    [ClientRpc]
   
    private void EnableMovementClientRpc(float startingRotationY)
    {
        if (IsOwner)
        {
            canMove = true;
            currentRotationY = startingRotationY; // Kameranın doğru yöne bakmasını sağlar
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            currentVerticalRotation = 0f;
            if (mainCamera != null) HandleCamera(true);
        }
    }

    private void Update()
    {
        if (!IsOwner || !canMove) return;

        HandleInput();
        HandleCamera(false);

        if (Input.GetMouseButtonDown(0)) TryKick();

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !canMove) return;
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
        Quaternion turnRotation = Quaternion.Euler(0f, currentRotationY, 0f);
        rb.MoveRotation(turnRotation);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 moveDir = (transform.forward * v) + (transform.right * h);
        if (moveDir.magnitude > 1f) moveDir.Normalize();

        Vector3 targetVelocity = moveDir * speed;
        // Y ekseni hızını (yerçekimi) koru, X ve Z'yi değiştir
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }

    private void HandleCamera(bool snapToPosition)
    {
        if (mainCamera == null) return;

        Quaternion cameraRotation = Quaternion.Euler(currentVerticalRotation, currentRotationY, 0);
        Vector3 targetPosition = transform.position + Vector3.up * 1.5f + (cameraRotation * cameraOffset);

        if (snapToPosition)
            mainCamera.transform.position = targetPosition;
        else
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, cameraSmoothness * Time.deltaTime);

        mainCamera.transform.LookAt(transform.position + Vector3.up * 1.5f);
    }

    private void CheckGround()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    private void TryKick()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, 2f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Ball"))
            {
                var ballNetObj = hit.GetComponent<NetworkObject>();
                if (ballNetObj != null) KickBallServerRpc(ballNetObj.NetworkObjectId, transform.forward);
            }
        }
    }

    [ServerRpc]
    private void KickBallServerRpc(ulong ballNetworkId, Vector3 direction)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ballNetworkId, out NetworkObject ballNetObj))
        {
            Rigidbody ballRb = ballNetObj.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                Vector3 kickDir = (direction + Vector3.up * 0.5f).normalized;
                ballRb.AddForce(kickDir * kickForce, ForceMode.Impulse);
            }
        }
    }
}