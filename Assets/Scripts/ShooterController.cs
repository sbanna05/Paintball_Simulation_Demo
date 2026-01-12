using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using StarterAssets;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

public class ShooterController : MonoBehaviour
{
    [Header("Camera & Sensitivity")]
    [SerializeField] private CinemachineVirtualCamera aimVirtualCamera;
    [SerializeField] private float normalSensitivity = 0.5f;
    [SerializeField] private float aimSensitivity = 0.8f;
    [SerializeField] private LayerMask aimColliderLayerMask = new LayerMask();

    [Header("Shooting")]
    [SerializeField] private Transform bullet;
    [SerializeField] private Transform spawnpoint;

    [Header("Rigging")]
    [SerializeField] private Rig aimRig;

    private ThirdPersonController thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;
    private Animator animator;
    private PaintballAgent paintballAgent; // Reference to agent (if controlled by ML)
    private float aimRigWeight;

    private void Awake()
    {
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
        paintballAgent = GetComponent<PaintballAgent>();
    }

    private void Update()
    {
        // Smooth rig weight transition
        aimRig.weight = Mathf.Lerp(aimRig.weight, aimRigWeight, Time.deltaTime * 20f);
        animator.SetLayerWeight(1, aimRig.weight);

        // Get aim point (screen center for agent, mouse for player)
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenter);
        Vector3 aimPoint = transform.position + transform.forward * 10f;

        if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimColliderLayerMask))
        {
            aimPoint = hit.point;
        }

        // AIM MODE ACTIVE
        if (starterAssetsInputs.aim)
        {
            aimVirtualCamera.gameObject.SetActive(true);
            thirdPersonController.setSensitivity(aimSensitivity);
            thirdPersonController.setRotateOnMove(false);
            aimRigWeight = 1f;
            animator.SetBool("canAim", true);

            // Character looks at aim point
            Vector3 lookDir = aimPoint - transform.position;
            lookDir.y = 0;
            transform.forward = Vector3.Lerp(transform.forward, lookDir.normalized, Time.deltaTime * 20f);

            // SHOOT
            if (starterAssetsInputs.shoot)
            {
                Vector3 shootDir = (aimPoint - spawnpoint.position).normalized;

                // Instantiate bullet
                Transform bulletInstance = Instantiate(bullet, spawnpoint.position, Quaternion.LookRotation(shootDir, Vector3.up));

                // Set bullet owner (for ML-Agents)
                if (paintballAgent != null)
                {
                    Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
                    if (bulletScript != null)
                    {
                        bulletScript.SetOwner(paintballAgent);
                    }
                }

                starterAssetsInputs.shoot = false;
                animator.SetTrigger("shoot");
            }
        }
        // NORMAL MODE
        else
        {
            aimRigWeight = 0f;
            aimVirtualCamera.gameObject.SetActive(false);
            thirdPersonController.setSensitivity(normalSensitivity);
            thirdPersonController.setRotateOnMove(true);
            animator.SetBool("canAim", false);
            starterAssetsInputs.shoot = false;
        } 
    }


    public void SetRigWeightImmediate(float weight)
    {
        aimRigWeight = weight;
        if (aimRig != null) aimRig.weight = weight;
    }

}