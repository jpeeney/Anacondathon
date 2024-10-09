using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace TarodevController
{
    /// <summary>
    /// Hey!
    /// Tarodev here. I built this controller as there was a severe lack of quality & free 2D controllers out there.
    /// I have a premium version on Patreon, which has every feature you'd expect from a polished controller. Link: https://www.patreon.com/tarodev
    /// You can play and compete for best times here: https://tarodev.itch.io/extended-ultimate-2d-controller
    /// If you hve any questions or would like to brag about your score, come to discord: https://discord.gg/tarodev
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private ScriptableStats _stats;
        [SerializeField] private GameObject _snake;
        private Rigidbody2D _rb;
        private CapsuleCollider2D _col;
        private SpriteRenderer _rend;
        private FrameInput _frameInput;
        private Vector2 _frameVelocity;
        private bool _cachedQueryStartInColliders;
        [SerializeField] private int _playerFacing = 1;
        [SerializeField] private int _playerLooking = 0;

        #region Interface

        public Vector2 FrameInput => _frameInput.Move;
        
        // Events are like Scratch broadcast messages, but you must specify the receivers (subscribers). These receivers can also unsubscribe.
        public event Action<bool, float> GroundedChanged;
        public event Action Jumped;
        // You'll want a grappling event here with a bool (if it's happening or not) another bool (if it's serpentine or not).
            // When invoked with the first bool being true, the snake will start its movement. If false, the snake disappears.
                // If the snake collides with a latchable surface, the snake invokes a Pull event with true. The player controller is the only subscriber, and handles its movement to the snake's head.
                    // Player controller should diable all controllable movement and gravity.
                    // When the snake detects the player has reached its position, it invokes Pull with false. Player regains their movement and gravity, then player invokes grappling with false.
                // If the grapple needs to be canceled (damage or manual), invoke Pull with false. Player regains their movement and gravity, then player invokes grappling with false.
            // When invoked with true and true, subscribers disable their movement / gravity during a serpentine grapple. If false and true, subscribers re-enable their movement / gravity.
        public event Action<Grapple, int, int> GrappleChanged; //bool = start/stop, string = grapple type, int = _playerFacing, int = _playerLooking
  
        #endregion

        private float _time;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _rend = GetComponentInChildren<SpriteRenderer>();

            _clingStamina = _stats.MaxClingStamina;
            _grappleInUse = Grapple.None;

            var _eventPublisher = _snake.GetComponent<SnakeController>();
            _eventPublisher.PullingChanged += OnPullingChanged;

            _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            GatherInput();
            
            // Print any debug values you need here
            //Debug.Log(_frameVelocity + " : " + _rb.velocity);

        }

        #region Input

        private void GatherInput()
        {
            _frameInput = new FrameInput
            {
                JumpDown = Input.GetButtonDown("Jump"),
                JumpHeld = Input.GetButton("Jump"),
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Grapple = Input.GetButtonDown("Grapple"),
                Cling = Input.GetButton("Cling")
            };

            if (_stats.SnapInput) //Makes all Input snap to an integer. Prevents gamepads from walking slowly. Recommended value is true to ensure gamepad/keyboard parity.
            {
                _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.x);
                _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.y);
            }

            _clingButtonDown = _frameInput.Cling;

            if ((_clinging || _sliding) && _frameInput.Move.x != _playerFacing)
            {
                _timeDirAwayFromWallPressed = _time;
            }

            // Save which directions the player is facing and looking for other classes to use
            // E.g. which direction the grapple should go and where it should start
            if (_frameInput.Move.x != 0) _playerFacing = (int)_frameInput.Move.x; // 1 = right, -1 = left
            if (_grappleInUse == Grapple.None) _playerLooking = (int)_frameInput.Move.y; // 1 = up, 0 = neither, -1 = down

            if (_frameInput.Grapple) _grappleInUse = Grapple.Slither;

            if (_frameInput.JumpDown)
            {
                _jumpToConsume = true;
                _timeJumpWasPressed = _time;
                if (_clinging || _sliding) _timeWallJumpStarted = _time;
            }
        }

        #endregion

        private void FixedUpdate()
        {
            CheckCollisions();

            HandleGrapple();

            if (!_beingPulled && _grappleInUse != Grapple.Slither)
            {
                HandleJump();
                if (!(_onRing && _frameVelocity.y == 0 && _playerLooking != -1))
                {
                    HandleDirection();
                    HandleGravity();
                }
            }

            ApplyMovement();
            ApplyVisuals(); // TEMPORARY - should be in PlayerAnimator
        }

        #region Collisions
        
        private float _frameLeftGrounded = float.MinValue;
        private float _frameCeilingHit = float.MinValue;
        private bool _grounded;
        private bool _clingButtonDown;
        private bool _clinging;
        private bool _sliding;
        private int _previousWallDirection;
        private bool _onRing;

        private void CheckCollisions()
        {

            // ~ is the bitwise NOT operator. In the folllowing code, every layer counts for collision except the PlayerLayer itself.
            // You could easily specify more layers in the ScriptableStats like ground, death, checkpoint, etc.

            Physics2D.queriesStartInColliders = false;

            // Add death collision here? Currently controlled by the damager

            bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);

            bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);
            bool leftHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.left, _stats.GrounderDistance, ~_stats.PlayerLayer);
            bool rightHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.right, _stats.GrounderDistance, ~_stats.PlayerLayer);

            bool grappleCeilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrappleDistance, ~_stats.PlayerLayer);
            bool grappleLeftHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.left, _stats.GrappleDistance, ~_stats.PlayerLayer);
            bool grappleRightHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.right, _stats.GrappleDistance, ~_stats.PlayerLayer);            

            // Hit a Ceiling
            if (ceilingHit) 
            {
                _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);
                _frameCeilingHit = _time;
            }

            // Landed on the Ground
            if (!_grounded && groundHit)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _bufferedWallJumpUsable = true;
                _endedJumpEarly = false;
                _clinging = false;
                _clingStamina = _stats.MaxClingStamina;
                _sliding = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
            }
            // Left the Ground
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }
            
            if (_grappleInUse != Grapple.None)
            {
                // Enough room to use the grapple?
                bool grappleCramped = ((_playerLooking == 0 && ((_playerFacing == 1 && grappleRightHit && !((_sliding || _clinging) && _previousWallDirection == 1)) || (_playerFacing == -1 && grappleLeftHit && !((_sliding || _clinging) && _previousWallDirection == -1))))
                || (_playerLooking == 1 && grappleCeilingHit) || (_playerLooking == -1 && groundHit));

                // Player hit a surface while grappling?
                bool reachedGrappledSurface = ((_playerLooking == 0 && ((_playerFacing == 1 && rightHit && !((_sliding || _clinging) && _previousWallDirection == 1)) || (_playerFacing == -1 && leftHit && !((_sliding || _clinging) && _previousWallDirection == -1))))
                || (_playerLooking == 1 && ceilingHit) || (_playerLooking == -1 && groundHit));
                
                if ((_beingPulled && reachedGrappledSurface) || (!_beingPulled && grappleCramped) || _onRing)
                {
                    GrappleChanged?.Invoke(Grapple.None, _playerFacing, _playerLooking);
                    if (ceilingHit && _clingButtonDown && _clingStamina > 0)
                    {
                        _clinging = true;
                        _frameVelocity.y = 0;  
                    }
                }

            }
            else if (_grounded ||  _refreshToBeConsumed)
            {
                _refreshToBeConsumed = false;
                _grappleUsable = true;
            }
            
            if (!_grounded && !_beingPulled) //wall cling, slide, and buffer
            {
                if (rightHit)
                {
                    _previousWallDirection = 1;
                    _bufferedWallJumpUsable = true;
                }
                else if (leftHit)
                {
                    _previousWallDirection = -1;
                    _bufferedWallJumpUsable = true;
                }

                if (_clingButtonDown && _clingStamina > 0 && (((rightHit || leftHit) && _frameVelocity.y <= 0)
                || ceilingHit || _time < _frameCeilingHit + _stats.CeilingClingGracePeriod))
                {
                    _clinging = true;
                    if (rightHit || leftHit) _frameVelocity.y = 0;
                    else if (ceilingHit)
                    {
                        _frameVelocity = Vector2.zero;
                        _previousWallDirection = -_playerFacing; // Cancels out in ceiling jump so that you jump in the direction you're facing / pressing
                    }
                    else 
                    {
                        _frameVelocity = new Vector2(0, _stats.JumpPower); // If you press the cling button a little too late after hitting the ceiling, boost the player back up to the ceiling
                        _previousWallDirection = -_playerFacing;
                    }
                }
                else
                {
                    _clinging = false;
                    if ((rightHit || leftHit) && _frameVelocity.y < 0) _sliding = true;
                    else _sliding = false;

                }
            }

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.tag == "Ring")
            {
                if (!_onRing)
                {
                    if (_grappleInUse != Grapple.None) GrappleChanged?.Invoke(Grapple.None, _playerFacing, _playerLooking);
                    _onRing = true;
                    transform.position = new Vector2(collision.transform.position.x, collision.transform.position.y - _rend.bounds.size.y);
                    _frameVelocity = Vector2.zero;
                }
            }
        }

        void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.gameObject.tag == "Ring")
            {
                _onRing = false;
                RefreshGrapple();
            }
        }

        #endregion

        #region Jumping

        private bool _jumpToConsume;
        private bool _bufferedJumpUsable;
        private bool _bufferedWallJumpUsable;
        private bool _endedJumpEarly;
        private bool _coyoteUsable;
        private float _timeJumpWasPressed;
        private float _timeWallJumpStarted;

        private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + _stats.JumpBuffer;
        private bool HasBufferedWallJump => _bufferedWallJumpUsable && _time < _timeWallJumpStarted + _stats.JumpBuffer;
        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

        private void HandleJump()
        {
            if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.velocity.y > 0) _endedJumpEarly = true;

            if (!_jumpToConsume && !HasBufferedJump && !HasBufferedWallJump) return;

            if (_grounded || CanUseCoyote || _sliding || _clinging || _onRing) ExecuteJump(); // can jump from ring

            _jumpToConsume = false;
        }

        private void ExecuteJump()
        {
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _bufferedWallJumpUsable = false;
            _coyoteUsable = false;
            _frameVelocity.y = _stats.JumpPower;
            if (_sliding || _clinging)
            {
                _sliding = false;
                _clinging = false;
                _frameVelocity.x = _stats.WallJumpPower * _previousWallDirection * -1;
            }
            Jumped?.Invoke();
        }

        #endregion

        #region Horizontal
        private float _timeDirAwayFromWallPressed;
        private bool HasLeaveWallBuffer => _clinging || (_sliding && !_clinging && _frameInput.Move.x != _previousWallDirection && _time < _timeDirAwayFromWallPressed + _stats.LeaveWallBuffer);
        private bool HoldingTowardWallJump => !_grounded && _frameInput.Move.x == _previousWallDirection && _time < _timeWallJumpStarted + _stats.WallJumpTimer;
        private void HandleDirection()
        {
            // Knockback from damage should override wall jump
            if (!_inKnockback && _clinging)
            {
                _frameVelocity.x = 0;
            }
            else if (_frameInput.Move.x == 0 || _inKnockback || HoldingTowardWallJump)
            {
                var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
                if (_frameVelocity.x == 0) 
                {
                    _inKnockback = false;
                }
            }
            else if (HasLeaveWallBuffer) return;
            else
            {
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * _stats.MaxSpeed, _stats.Acceleration * Time.fixedDeltaTime);
                //_frameVelocity.x = Mathf.Clamp(Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * _stats.MaxSpeed, _stats.Acceleration * Time.fixedDeltaTime), -_stats.MaxSpeed, _stats.MaxSpeed);
            } 
        }

        #endregion

        #region Knockback
        private bool _inKnockback;
        public void HandleKnockback(Vector2 force)
        {
            _inKnockback = true;
            _frameVelocity = new Vector2 (force.x * _playerFacing * -1, force.y);
            //Debug.Log("Received knockback pulse");
        }

        #endregion

        #region Gravity
        [SerializeField] private float _clingStamina;

        private void HandleGravity()
        {
            if (_clinging)
            {
                _clingStamina -= Time.fixedDeltaTime;
            }
            else if (_grounded && _frameVelocity.y <= 0f)
            {
                _frameVelocity.y = _stats.GroundingForce;
            }
            else
            {
                if (_sliding) 
                {
                    _frameVelocity.y = -_stats.WallSlideSpeed;
                }
                else 
                {
                    var inAirGravity = _stats.FallAcceleration;
                    if (_endedJumpEarly && _frameVelocity.y > 0) inAirGravity *= _stats.JumpEndEarlyGravityModifier;
                    _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -_stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
                }
            }
        }

        #endregion

        #region Grapple

        [SerializeField] private bool _grappleUsable;
        // Consider consolidating the below variables into an enum state variable, bc you'll need one for clinging too and for serpentine grappling freeze too
        [SerializeField] private Grapple _grappleInUse;
        [SerializeField] private bool _beingPulled;
        [SerializeField] private bool _refreshToBeConsumed;
        [SerializeField] private List<Vector2> pivotPoints;

        private void HandleGrapple()
        {
            if (_grappleInUse != Grapple.None && _grappleUsable)
            {
                //Check if it's a serpentine grapple
                //Set a bool to freeze player in the air
                _grappleUsable = false;
                var playerFacing = _sliding || _clinging ? -_previousWallDirection : _playerFacing; // If you're on a wall, grapple away from the wall
                if (_grappleInUse == Grapple.Slither) _frameVelocity = Vector2.zero;
                _snake.SetActive(true);
                GrappleChanged?.Invoke(_grappleInUse, playerFacing, _playerLooking);
            }

            if (_beingPulled)
            {
                if (_grappleInUse == Grapple.Quick) // when this becomes a coroutine, you'll have to change some ifs to whiles and add yield returns
                {
                    if (_playerLooking == 0)
                    {
                        if (Math.Abs(transform.position.x - _snake.transform.position.x) > 0.5) // Move the player toward the wall
                        {
                            if (transform.position.x < _snake.transform.position.x) transform.Translate(Vector2.right * _stats.GrappleSpeed * Time.fixedDeltaTime);
                            else transform.Translate(Vector2.left * _stats.GrappleSpeed * Time.fixedDeltaTime);
                        }
                        else 
                        {
                            transform.position = new Vector2 (_snake.transform.position.x, transform.position.y); // Snap the player to the wall
                            GrappleChanged?.Invoke(Grapple.None, _playerFacing, _playerLooking);
                        }
                    }
                    else
                    {
                        if (_playerLooking == -1 && _grounded) GrappleChanged?.Invoke(Grapple.None, _playerFacing, _playerLooking);
                        else
                        {
                            if (Math.Abs(transform.position.y - _snake.transform.position.y) > 0.5) // Move the player toward the ceiling / floor
                            {
                                if (transform.position.y < _snake.transform.position.y) transform.Translate(Vector2.up * _stats.GrappleSpeed * Time.fixedDeltaTime);
                                else transform.Translate(Vector2.down * _stats.GrappleSpeed * Time.fixedDeltaTime);
                            }
                            else 
                            {
                                transform.position = _snake.transform.position; // Snap the player to the ceiling / floor
                                GrappleChanged?.Invoke(Grapple.None, _playerFacing, _playerLooking);                            
                            }
                        }
                    } 
                }    
            }
        }

        public void RefreshGrapple()
        {
            _refreshToBeConsumed = true;
        }

        private IEnumerator SlitherPull()
        {
            for (int i = 0; i < pivotPoints.Count; i++)
            {
                while (_beingPulled && (transform.position.x != pivotPoints[i].x || transform.position.y != pivotPoints[i].y))
                {
                    int horiz = 0;
                    int vert = 0;

                    if (Math.Abs(transform.position.x - pivotPoints[i].x) > 0.5)
                    {
                        if (transform.position.x < pivotPoints[i].x) horiz = 1;
                        else horiz = -1;
                    }
                    else transform.position = new Vector2(pivotPoints[i].x, transform.position.y);

                    if (Math.Abs(transform.position.y - pivotPoints[i].y) > 0.5)
                    {
                        if (transform.position.y < pivotPoints[i].y) vert = 1;
                        else vert = -1;
                    }
                    else transform.position = new Vector2(transform.position.x, pivotPoints[i].y);

                    transform.Translate(new Vector2(horiz, vert) * _stats.GrappleSpeed * Time.fixedDeltaTime);
                    yield return null;
                }
                if (_beingPulled) yield return null;
            }
            if (_beingPulled) GrappleChanged?.Invoke(Grapple.None, _playerFacing, _playerLooking);
        }

        #endregion

        private void ApplyMovement() => _rb.velocity = _frameVelocity;

        private void ApplyVisuals() // You should move this to the PlayerAnimator, same with the iFrame visuals. TEMPORARY
        {
            var value = _clingStamina / _stats.MaxClingStamina;
            _rend.color = new Color(_rend.color.r, value, value, _rend.color.a);

        }

        #region Event Handlers

        private void OnPullingChanged(bool pulling, List<Vector2> snakePivotPoints)
        {
            _beingPulled = pulling;
            if (!_beingPulled) // pulling just finished
            {
                _grappleInUse = Grapple.None;
                _snake.SetActive(false);
                // If cling in queue, move to cling behavior
            }
            else // pulling just started
            {
                _frameVelocity = Vector2.zero;
                if (_grappleInUse == Grapple.Slither)
                {
                    pivotPoints = snakePivotPoints;
                    StartCoroutine(SlitherPull());
                }
                else if (_grappleInUse == Grapple.Quick)
                {
                    // start that coroutine
                }
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_stats == null) Debug.LogWarning("Please assign a ScriptableStats asset to the Player Controller's Stats slot", this);
        }
#endif
    }

    public enum Grapple
    {
        None, Quick, Slither
    }
    
    public struct FrameInput
    {
        public bool JumpDown;
        public bool JumpHeld;
        public Vector2 Move;
        public bool Grapple;
        public bool Cling;
    }

    public interface IPlayerController
    {
        public event Action<bool, float> GroundedChanged;
        public event Action Jumped;
        public event Action<Grapple, int, int> GrappleChanged;
        public Vector2 FrameInput { get; }
    }
}