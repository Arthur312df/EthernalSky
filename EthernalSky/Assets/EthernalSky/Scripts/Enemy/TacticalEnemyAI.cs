using System.Collections;
using UnityEngine;

public class TacticalEnemyAI : NetworkObject, IDamageable
{
    private enum AIState { Chase, WindUp, Recovery, Dead }

    [Header("Atributos Vitais")]
    [SerializeField] private string enemyName = "Guardião Corrompido";
    [SerializeField] private float maxHp = 120f;
    private float currentHp;

    [Header("Movimentação e Leitura de Padrão")]
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float engageDistance = 2.6f;
    [SerializeField] private float telegraphDuration = 0.9f;
    [SerializeField] private float recoveryDuration = 1.1f;

    [Header("Configuração do Golpe Telegrafado")]
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackRange = 3.2f;
    [SerializeField] private float attackWidth = 1.6f;

    private AIState currentState = AIState.Chase;
    private Transform playerTarget;
    private Renderer meshRenderer;
    private Color originalColor;

    private Transform healthBarRoot;
    private Transform healthBarFill;
    private Camera mainCam;

    private Transform telegraphRoot;
    private Transform telegraphFill;

    [Header("Loot / Drop ao Morrer")]
    [SerializeField] private GameObject[] itemDropPrefabs; // Lista de prefabs de itens que ele pode dropar

    private void Awake()
    {
        currentHp = maxHp;
        mainCam = Camera.main;
        meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }

        CreateFloatingHealthBar();
        CreateTelegraphIndicator();
    }

    private void Start()
    {
        Initialize(System.Guid.NewGuid().ToString(), false);

        var player = FindFirstObjectByType<IsometricPlayerController>();
        if (player != null)
        {
            playerTarget = player.transform;
        }
    }

    private void Update()
    {
        if (currentState == AIState.Dead || playerTarget == null) return;

        if (currentState == AIState.Chase)
        {
            HandleChaseState();
        }
    }

    private void LateUpdate()
    {
        if (healthBarRoot != null && mainCam != null)
        {
            healthBarRoot.rotation = mainCam.transform.rotation;
        }
    }

    private void HandleChaseState()
    {
        Vector3 dirToPlayer = playerTarget.position - transform.position;
        dirToPlayer.y = 0f;
        float distance = dirToPlayer.magnitude;

        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        if (distance <= engageDistance)
        {
            StartCoroutine(ExecuteAttackPatternRoutine());
        }
        else
        {
            transform.position += dirToPlayer.normalized * moveSpeed * Time.deltaTime;
        }
    }

    private IEnumerator ExecuteAttackPatternRoutine()
    {
        currentState = AIState.WindUp;

        Vector3 lockDir = playerTarget.position - transform.position;
        lockDir.y = 0f;
        if (lockDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(lockDir.normalized);
        }

        telegraphRoot.gameObject.SetActive(true);
        float elapsed = 0f;

        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / telegraphDuration);

            telegraphFill.localScale = new Vector3(attackWidth, 0.02f, attackRange * progress);
            telegraphFill.localPosition = new Vector3(0f, 0.01f, (attackRange * progress) * 0.5f);

            yield return null;
        }

        telegraphRoot.gameObject.SetActive(false);

        Vector3 boxCenter = transform.position + Vector3.up * 0.5f + transform.forward * (attackRange * 0.5f);
        Vector3 boxHalfExtents = new Vector3(attackWidth * 0.5f, 0.8f, attackRange * 0.5f);

        Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, transform.rotation);
        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            if (hit.TryGetComponent<PlayerCombatController>(out PlayerCombatController playerCombat))
            {
                playerCombat.TakeDamage(attackDamage, transform.forward);
            }
        }

        currentState = AIState.Recovery;
        yield return new WaitForSeconds(recoveryDuration);

        currentState = AIState.Chase;
    }

    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        if (currentState == AIState.Dead) return;

        currentHp = Mathf.Max(0f, currentHp - amount);
        UpdateFloatingHealthBar();

        if (currentHp <= 0f)
        {
            Die();
            return;
        }

        if (currentState != AIState.WindUp)
        {
            transform.position += hitDirection.normalized * 0.12f;
        }

        if (meshRenderer != null)
        {
            StartCoroutine(FlashHitColor());
        }
    }

   private void Die()
    {
        currentState = AIState.Dead;
        StopAllCoroutines();

        if (telegraphRoot != null)
        {
            telegraphRoot.gameObject.SetActive(false);
        }

        if (TryGetComponent<Collider>(out Collider col))
        {
            col.enabled = false;
        }

        // Instancia o item no chão na posição atual do inimigo
        SpawnLoot();

        Debug.Log($"[Combate] {enemyName} foi derrotado e removido da cena!");
        Destroy(gameObject, 0.15f);
    }

    private void SpawnLoot()
    {
        if (itemDropPrefabs != null && itemDropPrefabs.Length > 0)
        {
            int randomIndex = Random.Range(0, itemDropPrefabs.Length);
            GameObject prefabToDrop = itemDropPrefabs[randomIndex];

            if (prefabToDrop != null)
            {
                // Posição inicial na altura do inimigo
                Vector3 spawnPos = transform.position;

                // Joga um raio para baixo para encontrar o chão exato da arena isométrica
                if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 10.0f))
                {
                    spawnPos = hit.point; // Ponto exato onde o raio bateu no chão
                }

                Instantiate(prefabToDrop, spawnPos, Quaternion.identity);
                Debug.Log($"[Loot] Item dropado e posicionado no chão com sucesso!");
            }
        }
    }
    private void CreateTelegraphIndicator()
    {
        telegraphRoot = new GameObject("TelegraphZone").transform;
        telegraphRoot.SetParent(transform, false);
        telegraphRoot.localPosition = new Vector3(0f, -0.95f, 0f);

        GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        border.transform.SetParent(telegraphRoot, false);
        border.transform.localScale = new Vector3(attackWidth, 0.01f, attackRange);
        border.transform.localPosition = new Vector3(0f, 0f, attackRange * 0.5f);
        Destroy(border.GetComponent<Collider>());
        border.GetComponent<Renderer>().material.color = new Color(0.35f, 0.05f, 0.05f, 1f);

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.transform.SetParent(telegraphRoot, false);
        fill.transform.localScale = new Vector3(attackWidth, 0.02f, 0.01f);
        Destroy(fill.GetComponent<Collider>());
        fill.GetComponent<Renderer>().material.color = new Color(0.95f, 0.15f, 0.15f, 1f);

        telegraphFill = fill.transform;
        telegraphRoot.gameObject.SetActive(false);
    }

    private void CreateFloatingHealthBar()
    {
        healthBarRoot = new GameObject("FloatingHealthBar").transform;
        healthBarRoot.SetParent(transform, false);
        healthBarRoot.localPosition = new Vector3(0f, 1.4f, 0f);

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.transform.SetParent(healthBarRoot, false);
        bg.transform.localScale = new Vector3(1.2f, 0.15f, 0.02f);
        Destroy(bg.GetComponent<Collider>());
        bg.GetComponent<Renderer>().material.color = new Color(0.1f, 0.1f, 0.1f);

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.transform.SetParent(healthBarRoot, false);
        fill.transform.localPosition = new Vector3(0f, 0f, -0.015f);
        fill.transform.localScale = new Vector3(1.16f, 0.11f, 0.02f);
        Destroy(fill.GetComponent<Collider>());
        fill.GetComponent<Renderer>().material.color = new Color(0.85f, 0.15f, 0.15f);

        healthBarFill = fill.transform;
    }

    private void UpdateFloatingHealthBar()
    {
        if (healthBarFill == null) return;
        float ratio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        healthBarFill.localScale = new Vector3(1.16f * ratio, 0.11f, 0.02f);
        healthBarFill.localPosition = new Vector3(-0.58f * (1f - ratio), 0f, -0.015f);
    }

    private IEnumerator FlashHitColor()
    {
        meshRenderer.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        meshRenderer.material.color = originalColor;
    }
}