using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    enum playerState
    {
        Idling,
        Walking,
        Running,
        Jumping,
        Falling,
        DoubleJumping,
        Crouching
    }

    private playerState currentState = playerState.Idling;
    
    [Header("-- Reference --")] 
    [SerializeField] private Rigidbody rb;
    [SerializeField] private GameObject playerCamera;
    [SerializeField] private LayerMask groundLayer;

    [FormerlySerializedAs("playerMovementSpeed")]
    [Header("-- Character Parameters --")] 
    [SerializeField] private float playerMovementForce = 5f;
    [SerializeField] private float playerJumpForce = 5f;
    [SerializeField] private float playerDoubleJumpForce = 15f;
    [SerializeField] private const float CAMERA_UPPER_LIMIT = 80f;
    [SerializeField] private const float CAMERA_BOTTOM_LIMIT = -80f;
    [SerializeField] private Vector2 cameraMoveSpeed = new Vector2(1, 1);
    [SerializeField] private Vector2Int axisInversion = new Vector2Int(1, 1);
    


    private PlayerControls PIC;
    private Vector2 movementInput;
    private Vector2 rotateInput;
    private Vector2 playerDirection;

    private float currentFacingDegree = 0;
    private float cameraAngle = 0;
    private float currentMomentum = 0;

    private bool runHold = false;
    private bool doubleJumpAvaliable = true;
    private bool firstJumpLanuched = false;
    private bool crouchHold = false;
    
    private void Awake()
    {
        PIC = new PlayerControls();
        PIC.PlayerInput.Move.performed += ctx => movementInput = ctx.ReadValue<Vector2>();
        PIC.PlayerInput.CameraMove.performed += ctx2 => rotateInput = ctx2.ReadValue<Vector2>();
    }

    private void OnEnable()
    {
        PIC.Enable();
    }

    private void OnDisable()
    {
        PIC.Disable();
    }

    private void Update()
    {
        DetectRunHold();
        DetectCrouchHold();
        MakeJump();
    }

    private void LateUpdate()
    {
        // Calculate how much should camera move/player rotating
        Vector2 camDelta = new Vector2(
            rotateInput.x * cameraMoveSpeed.x * axisInversion.x * Time.deltaTime,
            rotateInput.y * cameraMoveSpeed.y * axisInversion.y * Time.deltaTime);
        turnPlayerAroundOnX(camDelta.x);
        turnCameraOnY(camDelta.y);
    }

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case playerState.Idling:
                if (!isGrounded())
                {
                    currentState = playerState.Falling;
                }

                if (rb.velocity != Vector3.zero || movementInput != Vector2.zero)
                {
                    if (runHold)
                    {
                        currentState = playerState.Running;
                    }
                    else
                    {
                        currentState = playerState.Walking;
                    }
                }

                break;

            case playerState.Jumping:
                // TODO: make player jump
                if (isGrounded())
                {
                    rb.AddForce(Vector3.up * playerJumpForce, ForceMode.Impulse);
                    currentState = playerState.Falling; // Transition to Falling after the jump
                    firstJumpLanuched = true;
                }
                break;
            
            case playerState.DoubleJumping:
                // make player able to double jump if available 
                rb.AddForce(Vector3.up * playerDoubleJumpForce, ForceMode.Impulse);
                doubleJumpAvaliable = false;
                currentState = playerState.Falling;
                break;
                
            case playerState.Falling:
                if (isGrounded())
                {
                    currentState = playerState.Walking;
                }
                playerMoving(0.7f);
                break;
            
            case playerState.Walking:
                if (rb.velocity == Vector3.zero && movementInput != Vector2.zero)
                {
                    currentState = playerState.Idling;
                }

                if (runHold)
                {
                    currentState = playerState.Running;
                }

                if (crouchHold)
                {
                    currentState = playerState.Crouching;
                }

                if (rb.velocity.y < -0.1)
                {
                    currentState = playerState.Falling;
                }

                doubleJumpAvaliable = true;
                firstJumpLanuched = false;
                playerMoving(1f);
                break;

            case playerState.Running:
                if (!runHold)
                {
                    currentState = playerState.Walking;
                }
                playerMoving(2f);
                break;

            case playerState.Crouching:
                transform.localScale = new Vector3(1, 0.5f, 1);
                playerMoving(0.5f);
                if (!crouchHold)
                {
                    transform.localScale = new Vector3(1, 1f, 1); 
                    currentState = playerState.Walking;
                }
                break;
            // TODO: make player crouching
        }
    }

    private void playerMoving(float speedMultipiler)
    {
        // First, calculate force direction and magnitude
        Vector3 forceVector = (transform.right * movementInput.x + transform.forward * movementInput.y) *
                              playerMovementForce;
        rb.AddForce(forceVector * speedMultipiler, ForceMode.Force);
    }

    private void turnPlayerAroundOnX(float deltaX)
    {
        transform.Rotate(Vector3.up, deltaX);
        // TODO: loops?
    }

    private void turnCameraOnY(float deltaY)
    {
        cameraAngle = Mathf.Clamp(cameraAngle + deltaY, CAMERA_BOTTOM_LIMIT, CAMERA_UPPER_LIMIT);
        playerCamera.transform.localEulerAngles = new Vector3(-cameraAngle, 0f, 0f);
        // playerCamera.transform.Rotate(playerCamera.transform.right, deltaY);
        // Debug.DrawLine(playerCamera.transform.position, playerCamera.transform.position + playerCamera.transform.right * 5);
    }

    private bool isGrounded()
    {
        float groundDistanceCutoff = 0.05f;
        Vector3 startPosition = transform.position + Vector3.down * 1f;

        RaycastHit hit;
        if (Physics.Raycast(startPosition, Vector3.down, out hit, 5f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.distance < groundDistanceCutoff;
        }
        Debug.Log(hit.distance);
        return false;
    }

    private void DetectRunHold()
    {
        runHold = PIC.PlayerInput.Run.IsPressed();
    }

    private void DetectCrouchHold()
    {
        crouchHold = PIC.PlayerInput.Crouch.IsPressed();
    }

    private void MakeJump()
    {
        if (PIC.PlayerInput.Jump.WasPressedThisFrame())
        {
            if (!firstJumpLanuched)
            {
                currentState = playerState.Jumping;
            }

            if (firstJumpLanuched && doubleJumpAvaliable)
            {
                currentState = playerState.DoubleJumping;       
            }
        }
    }
}
