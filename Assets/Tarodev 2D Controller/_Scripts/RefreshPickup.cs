using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TarodevController
{
    public class RefreshPickup : MonoBehaviour
    {
        private bool _active = true;
        private SpriteRenderer _rend;
        [SerializeField] private GameObject _player;

        void Awake()
        {
            var _eventPublisher = GameObject.Find("Player Controller").GetComponent<PlayerController>();
            _eventPublisher.GroundedChanged += OnGroundedChanged;
            _rend = GetComponentInChildren<SpriteRenderer>();
            _rend.color = new Color(_rend.color.r, _rend.color.g, _rend.color.b, 1);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (_active && (collision.tag == "Player" || (collision.tag == "Snake" && !collision.GetComponent<SnakeController>()._retracting)))
            {
                _player.GetComponent<PlayerController>().RefreshGrapple();
                _active = false;
                _rend.color = new Color(_rend.color.r, _rend.color.g, _rend.color.b, 0.1f);
            }
        }

        private void OnGroundedChanged(bool playerGrounded, float playerYVel)
        {
            if (playerGrounded)
            {
                _active = true;
                _rend.color = new Color(_rend.color.r, _rend.color.g, _rend.color.b, 1);
            }

        }
    }
}