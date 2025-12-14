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
    [SerializeField] private float normalSensitivity;
    [SerializeField] private float aimSensitivity;
    [SerializeField] private LayerMask aimColliderLayerMask = new LayerMask();

    [Header("Shooting")]
    [SerializeField] private Transform bullet;
    [SerializeField] private Transform spawnpoint;

    [Header("Rigging")]
    [SerializeField] private Rig aimRig;

    private ThirdPersonController thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;
    private Animator animator;
    private float aimRigWeight;


    private void Awake()
    {
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();

    }

    private void Update()
    {
        normalSensitivity = 0.5f;
        aimSensitivity = 0.8f;
        aimRig.weight = Mathf.Lerp(aimRig.weight, aimRigWeight, Time.deltaTime * 20f);
        // animator.SetLayerWeight(1, 1f);
        animator.SetLayerWeight(1, aimRig.weight);

        Vector3 mouseWorldPosition = mouse3d.GetMouseWorldPosition();

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenter);

        Vector3 aimPoint = transform.forward * 10f;

        if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimColliderLayerMask))
        {
            aimPoint = hit.point;
        }


        if (starterAssetsInputs.aim)
        {
            aimVirtualCamera.gameObject.SetActive(true);
            thirdPersonController.setSensitivity(aimSensitivity);
            thirdPersonController.setRotateOnMove(false);
            aimRigWeight = 1f;

            animator.SetBool("canAim", true);

            // karakter igazítása a kamera irányába
            Vector3 lookDir = aimPoint - transform.position;
            lookDir.y = 0;
            transform.forward = Vector3.Lerp(transform.forward, lookDir.normalized, Time.deltaTime * 20f);

            if (starterAssetsInputs.shoot)
            {
                Vector3 shootDir = (aimPoint - spawnpoint.position).normalized;
                Instantiate(bullet, spawnpoint.position, Quaternion.LookRotation(shootDir, Vector3.up));
                starterAssetsInputs.shoot = false;
                animator.SetTrigger("shoot");
            }

        }
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

}
