// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using PampelGames.Shared.Tools;
using UnityEngine;

namespace PampelGames.GoreSimulator.Demo
{
    public class DemoFPSCharacterController : MonoBehaviour
    {
        public Camera playerCamera;
        public Rigidbody rb;
        public float moveSpeed = 5f;
        public float mouseSensitivity = 2f;

        public Animator pistolAnimator;
        public LayerMask enemyLayer = 0;
        public float pistolForce = 10f;

        public GameObject grenadePrefab;
        public float grenadeForce = 10f;

        private float verticalRotation;

        private const string Shoot = "Shoot";
        private static readonly int shoot = Animator.StringToHash(Shoot);


        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleRotation();
            HandleShooting();
            HandleGrenades();
        }

        private void HandleShooting()
        {
            if (Input.GetMouseButtonDown(0))
            {
                pistolAnimator.SetTrigger(shoot);
                
                // Casting a ray in center of screen, trying to find IGoreObjects
                var ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
                if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, enemyLayer))
                {
                    if (hitInfo.collider.TryGetComponent<IGoreObject>(out var goreObject))
                    {
                        var forceDirection = (hitInfo.point - pistolAnimator.transform.position).normalized;
                        goreObject.ExecuteCut(hitInfo.point, forceDirection * pistolForce);
                    }
                }
            }
        }

        private void HandleGrenades()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                // Utilizing the internal pool. Returned to the pool in the DemoFPSGrenade script.
                var grenade = PGPool.Get(grenadePrefab);
                grenade.transform.localScale = Vector3.one * 0.25f;
                grenade.transform.position = pistolAnimator.transform.position;
                var forceDirection = pistolAnimator.transform.forward;
                var rigid = grenade.GetComponent<Rigidbody>();
                rigid.AddForce(forceDirection * grenadeForce);
            }
        }


        private void FixedUpdate()
        {
            HandleMovement();
        }

        private void HandleMovement()
        {
            var horizontalInput = Input.GetAxis("Horizontal");
            var verticalInput = Input.GetAxis("Vertical");

            var movement = new Vector3(
                transform.right.x * horizontalInput + transform.forward.x * verticalInput,
                0f,
                transform.right.z * horizontalInput + transform.forward.z * verticalInput);

            movement = movement.normalized * moveSpeed;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) movement *= 2f;

            var newPosition = rb.position + movement * Time.fixedDeltaTime;
            newPosition.y = rb.position.y;
            rb.MovePosition(newPosition);
        }


        private void HandleRotation()
        {
            var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }
}