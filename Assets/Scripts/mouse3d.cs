using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mouse3d : MonoBehaviour
{
    public static mouse3d Instance { get; private set; }

    [SerializeField] private LayerMask mouseColliderLayerMask;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Update visual position of mouse cursor in 3D space
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, mouseColliderLayerMask))
        {
            transform.position = raycastHit.point;
        }
    }

    // Static method to get mouse world position from anywhere
    public static Vector3 GetMouseWorldPosition()
    {
        if (Instance != null)
        {
            return Instance.GetMouseWorldPosition_Instance();
        }
        return Vector3.zero;
    }

    private Vector3 GetMouseWorldPosition_Instance()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, mouseColliderLayerMask))
        {
            return raycastHit.point;
        }
        else
        {
            return Vector3.zero;
        }
    }
}