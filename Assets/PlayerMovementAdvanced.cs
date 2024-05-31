using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerMovementAdvanced : MonoBehaviour
{
    [Header("Movement")]
    private float moveSpeed;
    public float groundDrag;
    public float walkSpeed;
    public float sprintSpeed;
    
    public float slideSpeed;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;

    public float wallrunSpeed; // new
    
    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask whatIsGround;
    bool grounded;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;
    
    public Transform orientation;

    float horizontalInput;
    float verticalInput;

    Vector3 moveDirection;

    Rigidbody rb;

    public MovementState state;
    public enum MovementState
    {
        walking,
        sprinting,
        wallrunning, // new
        crouching,
        sliding,
        air
    }

    public bool crouching;
    public bool sliding;
    public bool wallrunning; // new

    // Parameters for ground check
    public float groundCheckDistance = 0.5f; // The distance to check for ground
    public int numRaycasts = 5; // Number of raycasts to cast
    public float slopeThreshold = 30f; // The maximum slope angle that is considered as walkable

    // Ground check variables
    //bool grounded;
    bool onSteepGround;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        readyToJump = true;

        startYScale = transform.localScale.y;
    }

    private void Update()
    {
        // Perform ground check
        GroundCheck();
        // ground check
        //grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        MyInput();
        SpeedControl();
        StateHandler();

        // handle drag
        if (grounded)
            rb.drag = groundDrag;
        else
            rb.drag = 0;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // when to jump
        if(Input.GetKeyUp(jumpKey) && !grounded){
            Debug.Log("Jumped, but not ground");
            Debug.Log("Jumped, onSteepGround is " + onSteepGround);
        }
        else if(Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            readyToJump = false;
            //SDebug.Log("Jumped, on ground");
            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }

        // start crouch
        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }

        // stop crouch
        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    private void StateHandler()
    {
        // Mode - Wallrunning
        if (wallrunning)
        {
            state = MovementState.wallrunning;
            desiredMoveSpeed = wallrunSpeed;
        }

        // Mode - Sliding
        if (sliding) // new
        {
            state = MovementState.sliding;

            if (OnSlope() && rb.velocity.y < 0.1f)
                desiredMoveSpeed = slideSpeed; 

            else
                desiredMoveSpeed = sprintSpeed;
        }

        // Mode - Crouching
        else if (Input.GetKey(crouchKey)) // change to else if
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed; // moveSpeed to desiredMoveSpeed
        }

        // Mode - Sprinting
        else if(grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed; // moveSpeed to desiredMoveSpeed
        }

        // Mode - Walking
        else if (grounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }

        // Mode - Air
        else
        {
            state = MovementState.air;
        }

        // check if desiredMoveSpeed has changed drastically
        if(Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 4f && moveSpeed != 0)
        {
            StopAllCoroutines();
            StartCoroutine(SmoothlyLerpMoveSpeed());
        }
        else
        {
            moveSpeed = desiredMoveSpeed;
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
    }

    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        // smoothly lerp movementSpeed to desired value
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);

            if (OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);

                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
                time += Time.deltaTime * speedIncreaseMultiplier;

            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
    }

    private void MovePlayer()
    {
        // calculate movement direction
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

            // since we turn off the gravity on slope
            // if the player is moving upwards which means its y velocity is greater than zero
            if (rb.velocity.y > 0)
            // we add a bit of downward force to keep the player constantly on the slope
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }

        // on ground
        else if(grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

        // in air
        else if(!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        // turn gravity off while on slope
        if (!wallrunning) rb.useGravity = !OnSlope();
    }

    private void SpeedControl()
    {
        // limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }

        // limiting speed on ground or in air
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            // limit velocity if needed
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    private void Jump()
    {
        exitingSlope = true;

        // reset y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }
    private void ResetJump()
    {
        readyToJump = true;

        exitingSlope = false;
    }

    public bool OnSlope()
    {
        if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            // Calculate the angle between the player's direction and the surface normal
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);

            // Determine if the angle is within the acceptable slope range
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    private void GroundCheck2()
    {
        grounded = false;
        onSteepGround = false;

        // Cast multiple rays downward from the player's position
        for (int i = 0; i < numRaycasts; i++)
        {
            float angle = i * (360f / numRaycasts); // Calculate angle for raycast direction
            Vector3 direction = Quaternion.AngleAxis(angle, transform.up) * -transform.forward; // Calculate raycast direction

            //groundCheckDistance = playerHeight * 0.5f + 0.2f;

            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, playerHeight * 0.5f + 0.2f, whatIsGround))
            //if (Physics.Raycast(transform.position, direction, out hit, groundCheckDistance, whatIsGround))
            {
                grounded = true;

                // Check if the slope angle exceeds the threshold
                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                if (slopeAngle > slopeThreshold)
                {
                    onSteepGround = true;
                    break; // Exit loop if a steep slope is detected
                }
            }
        }
    }

    private void GroundCheck() // work
    {
        // Cast a single raycast straight down from the player's position
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, playerHeight * 0.5f + 0.2f, whatIsGround))
        {
            grounded = true;

            // Check if the slope angle exceeds the threshold
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > maxSlopeAngle)
            {
                onSteepGround = true;
            }
            else
            {
                onSteepGround = false;
            }
        }
        else
        {
            grounded = false;
            onSteepGround = false;
        }
    }
}