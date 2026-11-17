using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class IsometricPlayerController : NetworkObject
{
    [Header("Movimentação Isométrica")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private LayerMask groundLayer = ~0; // Padrão: lê todas as layers

    private CharacterController controller;
    private Camera mainCamera;

    private Vector3 isoForward;
    private Vector3 isoRight;
    private bool isDashing;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        isoForward = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
        isoRight = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
    }

    private void Start()
    {
        Initialize(System.Guid.NewGuid().ToString(), true);

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

        if (isDashing) return;

        HandleMovement();
        HandleAimRotation();
    }

    // Lê a direção exata que o jogador está pressionando no WASD
    private Vector3 GetMoveInputDirection()
    {
        if (Keyboard.current == null) return Vector3.zero;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;

        return (isoRight * horizontal + isoForward * vertical).normalized;
    }

    private void HandleMovement()
    {
        Vector3 direction = GetMoveInputDirection();

        if (direction.magnitude >= 0.1f)
        {
            controller.Move(direction * moveSpeed * Time.deltaTime);
        }

        if (!controller.isGrounded)
        {
            controller.Move(Vector3.down * 9.81f * Time.deltaTime);
        }
    }

    private void HandleAimRotation()
    {
        Vector3 aimDir = GetMouseAimDirection();
        if (aimDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private Vector3 GetMouseAimDirection()
    {
        if (Mouse.current == null || mainCamera == null) return transform.forward;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Tenta pelo Collider do chão primeiro
        if (groundLayer.value != 0 && Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 dir = hit.point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) return dir.normalized;
        }

        // Fallback matemático: funciona sempre, mesmo sem Ground Layer configurada
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 dir = hitPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) return dir.normalized;
        }

        return transform.forward;
    }

    public void PerformDash(Vector3 fallbackDirection, float distance, float duration)
    {
        // 1º Prioridade: Direção que você está apertando no WASD agora
        Vector3 dashDir = GetMoveInputDirection();

        // 2º Prioridade: Se estiver parado sem apertar WASD, vai na direção do cursor do mouse
        if (dashDir.sqrMagnitude < 0.01f)
        {
            dashDir = GetMouseAimDirection();
        }

        // Gira o personagem instantaneamente para o lado do Dash
        if (dashDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dashDir);
        }

        float safeDistance = distance > 0.1f ? distance : 4f;
        float safeDuration = duration > 0.05f ? duration : 0.18f;

        StopAllCoroutines();
        StartCoroutine(DashRoutine(dashDir, safeDistance, safeDuration));
    }

    private IEnumerator DashRoutine(Vector3 direction, float distance, float duration)
    {
        isDashing = true;
        float elapsed = 0f;
        float dashSpeed = distance / duration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            controller.Move((direction * dashSpeed + Vector3.down * 9.81f) * Time.deltaTime);
            yield return null;
        }

        isDashing = false;
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