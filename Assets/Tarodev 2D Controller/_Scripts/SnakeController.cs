using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace TarodevController
{
    public class SnakeController : MonoBehaviour
    {
        [SerializeField] private GameObject _player;
        [SerializeField] private float _quickSpeed;
        [SerializeField] private float _slitherSpeed;
        [SerializeField] private float _quickMaxLength;
        [SerializeField] private float _slitherMaxLength;

        private bool _snaking;
        private string _grappleType;
        private int _playerFacing;
        private int _playerLooking;
        private Vector2 _playerPos;
        private Vector2 _playerCol;
        private Vector2 _origin;
        private Vector2 _totalOriginOffset;
        private bool _retracting;
        [SerializeField] private string _slitherDir;
        private float _totalDistanceSlithered; // running total of abs value of displacement, whether horizontal or vertical
        [SerializeField] private List<Vector2> _pivotPoints;

        public event Action<bool, List<Vector2>> PullingChanged;

        // Start is called before the first frame update
        void Awake()
        {
            var _eventPublisher = GameObject.Find("Player Controller").GetComponent<PlayerController>();
            _eventPublisher.GrappleChanged += OnGrappleChanged;
            _playerCol = _player.GetComponent<CapsuleCollider2D>().bounds.size;
        }

        // Update is called once per frame
        void Update()
        {
            if (_snaking)
            {
                if (_grappleType == "quick")
                {
                    SetOrigin();

                    if (_retracting)
                    {
                        if ((_playerLooking == 0 && transform.position.x * _playerFacing > _origin.x * _playerFacing) 
                        || (_playerLooking != 0 && transform.position.y * _playerLooking > _origin.y * _playerLooking)) // While length is not minimum
                        {
                            MoveSnake(-1); //retract
                        }
                        else
                        {
                            ChangePulling(false); //No surface hit
                        }
                    }
                    else
                    {
                        if ((_playerLooking == 0 && transform.position.x * _playerFacing < (_origin.x + _quickMaxLength * _playerFacing) * _playerFacing) 
                        || (_playerLooking != 0 && transform.position.y * _playerLooking < (_origin.y + _quickMaxLength * _playerLooking) * _playerLooking)) // While length is not full
                        {
                            MoveSnake(1); //extend
                        }
                        else
                        {
                            _retracting = true;
                        }
                    }
                }
                else if (_grappleType == "slither" && !_retracting)
                {
                    if (Math.Abs(_origin.x - transform.position.x) + Math.Abs(_origin.y - transform.position.y) < _slitherMaxLength)
                    {
                        if (_slitherDir != "left" && _slitherDir != "right" && Input.GetAxisRaw("Horizontal") != 0)
                        {
                            _slitherDir = Input.GetAxisRaw("Horizontal") > 0 ? "right" : "left";
                            _pivotPoints.Add(transform.position);
                        }
                        if (_slitherDir != "up" && _slitherDir != "down" && Input.GetAxisRaw("Vertical") != 0)
                        {
                            _slitherDir = Input.GetAxisRaw("Vertical") > 0 ? "up" : "down";
                            _pivotPoints.Add(transform.position);
                        }

                        if (_slitherDir == "right") transform.Translate(Vector2.right * _slitherSpeed * Time.deltaTime);
                        else if (_slitherDir == "left") transform.Translate(Vector2.left * _slitherSpeed * Time.deltaTime);
                        else if (_slitherDir == "up") transform.Translate(Vector2.up * _slitherSpeed * Time.deltaTime);
                        else if (_slitherDir == "down") transform.Translate(Vector2.down * _slitherSpeed * Time.deltaTime);
                    }
                    else
                    {
                        StartCoroutine(SlitherRetract());
                    }
                }
            }
        }

        private void MoveSnake(int direction) // gonna need to be different for slither grapple, might even be a separate function
        {
            if (_playerLooking == 0)
            {
                _totalOriginOffset += new Vector2(direction * _playerFacing, 0) * _quickSpeed * Time.deltaTime;
            }
            else
            {
                _totalOriginOffset += new Vector2(0, direction * _playerLooking) * _quickSpeed * Time.deltaTime;
            }
            transform.position = _origin + _totalOriginOffset; // Vector2.SmoothDamp()?
        } 

        private void ChangePulling(bool pulling)
        {
            _snaking = false;
            _retracting = false;
            PullingChanged?.Invoke(pulling, _pivotPoints);
        }

        private void SetOrigin()
        {
            _playerPos = _player.transform.position;
            if (_playerLooking == 0)
            {
                _origin = new Vector2(_playerPos.x + (_playerCol.x * _playerFacing), _playerPos.y + _playerCol.y / 2);
            }
            else if (_playerLooking == 1)
            {
                _origin = new Vector2(_playerPos.x, _playerPos.y + _playerCol.y / 2 + _playerCol.y);
            }
            else if (_playerLooking == -1)
            {
                _origin = new Vector2(_playerPos.x, _playerPos.y - + _playerCol.y / 2);
            }
        }

        #region Collision

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.tag != "Player" && _snaking && !_retracting)
            {
                if (collision.gameObject.tag == "Death")
                {
                    Debug.Log("Snake collided with death material");
                    var damager = collision.GetComponent<Damager>();
                    _player.GetComponent<PlayerHealth>().TakeDamage(damager.getDamage(), Vector2.zero);

                    if (_grappleType == "slither") StartCoroutine(SlitherRetract());
                    else _retracting = true;
                }
                else
                {
                    if (_grappleType == "slither") _pivotPoints.Add(transform.position);
                    ChangePulling(true); // surface hit
                }
            }
        }

        private IEnumerator SlitherRetract()
        {
            _retracting = true;
            for (int i = _pivotPoints.Count - 1; i >= 0; i--)
            {
                while (transform.position.x != _pivotPoints[i].x || transform.position.y != _pivotPoints[i].y)
                {
                    int horiz = 0;
                    int vert = 0;

                    if (Math.Abs(transform.position.x - _pivotPoints[i].x) > 0.5)
                    {
                        if (transform.position.x < _pivotPoints[i].x) horiz = 1;
                        else horiz = -1;
                    }
                    else transform.position = new Vector2(_pivotPoints[i].x, transform.position.y);

                    if (Math.Abs(transform.position.y - _pivotPoints[i].y) > 0.5)
                    {
                        if (transform.position.y < _pivotPoints[i].y) vert = 1;
                        else vert = -1;
                    }
                    else transform.position = new Vector2(transform.position.x, _pivotPoints[i].y);

                    transform.Translate(new Vector2(horiz, vert) * _quickSpeed * Time.fixedDeltaTime);
                    yield return null;
                }
                Debug.Log("Reached position " + i);
                yield return null;
            }
            ChangePulling(false);
        }

        #endregion

        #region Event Handlers

        private void OnGrappleChanged(string grappleType, int playerFacing, int playerLooking)
        {
            _grappleType = grappleType;

            if (_grappleType == "none")
            {
                ChangePulling(false); // May or may not have hit surface - end it
            }
            else
            {
                _playerFacing = playerFacing;
                _playerLooking = playerLooking;

                _totalOriginOffset = new Vector2(0,0);
                SetOrigin();
                transform.position = _origin; //Might not need this here? But just in case it spawns and instantly collides with something

                if (_grappleType == "slither")
                {
                    if (_playerLooking == 0) _slitherDir = _playerFacing == 1 ? "right" : "left";
                    else _slitherDir = _playerLooking == 1 ? "up" : "down";
                    _pivotPoints = new List<Vector2>();
                    _pivotPoints.Add(_origin);
                }

                _snaking = true;
            }
        }

        #endregion
    }
}