using System.Collections;
using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitDirection);
}

public class TrainingDummy : NetworkObject, IDamageable
{
    [Header("Atributos do Alvo")]
    [SerializeField] private string enemyName = "Boneco de Treino";
    [SerializeField] private float maxHp = 100f;
    private float currentHp;

    private Renderer meshRenderer;
    private Color originalColor;
    private Transform healthBarFill;
    private Transform healthBarRoot;
    private Camera mainCam;

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
    }

    private void Start()
    {
        Initialize(System.Guid.NewGuid().ToString(), false);
    }

    private void LateUpdate()
    {
        if (healthBarRoot != null && mainCam != null)
        {
            healthBarRoot.rotation = mainCam.transform.rotation;
        }
    }

    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        currentHp = Mathf.Max(0f, currentHp - amount);
        UpdateFloatingHealthBar();

        transform.position += hitDirection.normalized * 0.15f;

        if (meshRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashHitColor());
        }

        if (currentHp <= 0f)
        {
            currentHp = maxHp;
            UpdateFloatingHealthBar();
        }
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
        yield return new WaitForSeconds(0.12f);
        meshRenderer.material.color = originalColor;
    }
}