using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class IsometricPlayerController : NetworkObject
{
    [Header("Movimentação Isométrica")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private LayerMask groundLayer;

    private CharacterController controller;
    private Camera mainCamera;

    // Eixos corrigidos para a câmera isométrica em 45 graus
    private Vector3 isoForward;
    private Vector3 isoRight;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        isoForward = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
        isoRight = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
    }

    private void Start()
    {
        // Inicializa como dono local para testes
        Initialize(System.Guid.NewGuid().ToString(), true);

        // Se já houver um personagem carregado do Supabase, aplica a posição salva
        if (CharacterDataManager.Instance != null && CharacterDataManager.Instance.CurrentCharacter != null)
        {
            var data = CharacterDataManager.Instance.CurrentCharacter;
            controller.enabled = false;
            transform.position = new Vector3(data.PosX, data.PosY, data.PosZ);
            controller.enabled = true;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        HandleAimRotation();
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null) return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;

        Vector3 direction = (isoRight * horizontal + isoForward * vertical).normalized;

        if (direction.magnitude >= 0.1f)
        {
            controller.Move(direction * moveSpeed * Time.deltaTime);
        }

        // Gravidade simples para manter o personagem no chão
        if (!controller.isGrounded)
        {
            controller.Move(Vector3.down * 9.81f * Time.deltaTime);
        }
    }

    private void HandleAimRotation()
    {
        if (Mouse.current == null || mainCamera == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 lookDir = hit.point - transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    [ContextMenu("Salvar Posição Atual no Supabase")]
    public async void SaveCurrentPositionToDatabase()
    {
        if (CharacterDataManager.Instance != null)
        {
            await CharacterDataManager.Instance.SaveCharacterPositionAsync(transform.position);
        }
    }
}