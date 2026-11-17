using UnityEngine;

public class IsometricCamera : MonoBehaviour
{
    [Header("Alvo e Posicionamento")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(-8f, 10f, -8f);
    [SerializeField] private float smoothSpeed = 8f;

    private void Start()
    {
        // Garante a angulação isométrica padrão (30° inclinação, 45° rotação Y)
        transform.rotation = Quaternion.Euler(30f, 45f, 0f);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}